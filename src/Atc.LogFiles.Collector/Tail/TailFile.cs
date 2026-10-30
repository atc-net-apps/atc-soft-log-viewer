namespace Atc.LogFiles.Collector.Tail;

/// <summary>
/// Raises <see cref="LineAdded"/> for each complete line appended to a file.
/// </summary>
/// <remarks>
/// File system notifications and a slow backup poll both only request a read; at most one read runs at a time,
/// and it returns when the new content has been read, so an idle tailed file does not hold a thread.
/// A line is raised when its line break has been written, and a truncated or replaced file is read from the start.
/// A last line without a line break that was already in the file when tailing started is raised once the file has
/// stopped growing for <see cref="PartialLineQuietPeriod"/>, so a file that ends without one still shows its last line.
/// A partial line written while tailing waits for its line break: buffered loggers flush in the middle of a line.
/// <para>
/// The file is read by its path, and a logger can roll it (rename it and create a new one) at any moment, also
/// between a notification and the read. The first bytes of the file are kept, so a read that finds another file at
/// the path does not read it from the old position: it waits for the rename or delete notification, which first
/// reads the rest of the old file. Without one (truncated, or replaced by a copy), the file is read from the start.
/// Known limits: a new file that starts with the same 64 bytes as the old one (a fixed header) is not seen as another
/// file; and when the watcher drops the rename notification (a buffer overflow in a burst), the new file is read from
/// the start, but the rest of the old file is lost.
/// </para>
/// </remarks>
public class TailFile : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PartialLineQuietPeriod = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ReplacedFileGracePeriod = TimeSpan.FromMilliseconds(500);
    private static readonly byte[] Utf8Preamble = Encoding.UTF8.GetPreamble();

    private readonly FileInfo file;
    private readonly FileSystemWatcher fileWatcher;
    private readonly Timer pollTimer;

    // Not disposable on purpose: a read on a timer thread may still run while the tail is disposed
    private readonly ArrayBufferWriter<byte> partialLine = new();
    private readonly byte[] readBuffer = new byte[64 * 1024];
    private readonly byte[] head = new byte[64];
    private int headCount;
    private long replacedSinceTicks = -1;
    private long offset;
    private long? pendingStartOffset;
    private string? renamedToPath;
    private long numberOfLines;
    private long lastGrowthTicks;
    private long lengthAtStart;
    private int isReading;
    private int isReadRequested;
    private int isResetRequested;
    private volatile bool isRunning;
    private volatile bool isPaused;
    private bool isDisposed;

    public TailFile(
        FileInfo file,
        long lastLineNumber = 0,
        long? startOffset = null)
    {
        ArgumentNullException.ThrowIfNull(file);

        this.file = file;
        numberOfLines = lastLineNumber;
        pendingStartOffset = startOffset;

        fileWatcher = new FileSystemWatcher(file.Directory!.FullName)
        {
            Filter = file.Name,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };

        fileWatcher.Created += OnCreatedOrChanged;
        fileWatcher.Changed += OnCreatedOrChanged;
        fileWatcher.Deleted += OnDeletedOrRenamed;
        fileWatcher.Renamed += OnDeletedOrRenamed;
        fileWatcher.Error += OnWatcherError;

        pollTimer = new Timer(_ => RequestRead());
    }

    public event EventHandler<TailLineEventArgs>? LineAdded;

    public void Start()
    {
        // Start where the caller stopped reading, so lines written in between are not skipped
        file.Refresh();
        lengthAtStart = file.Exists ? file.Length : 0;
        offset = pendingStartOffset ?? lengthAtStart;
        pendingStartOffset = null;
        RememberHead();

        isPaused = false;
        isRunning = true;
        fileWatcher.EnableRaisingEvents = true;
        pollTimer.Change(PollInterval, PollInterval);
    }

    public void Stop()
    {
        isRunning = false;
        pollTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        fileWatcher.EnableRaisingEvents = false;
    }

    public void Pause()
        => isPaused = true;

    public void Resume()
    {
        isPaused = false;
        RequestRead();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            Stop();

            fileWatcher.Created -= OnCreatedOrChanged;
            fileWatcher.Changed -= OnCreatedOrChanged;
            fileWatcher.Deleted -= OnDeletedOrRenamed;
            fileWatcher.Renamed -= OnDeletedOrRenamed;
            fileWatcher.Error -= OnWatcherError;
            fileWatcher.Dispose();
            pollTimer.Dispose();
        }

        isDisposed = true;
    }

    private void OnCreatedOrChanged(
        object sender,
        FileSystemEventArgs e)
        => RequestRead();

    private void OnDeletedOrRenamed(
        object sender,
        FileSystemEventArgs e)
    {
        if (e is RenamedEventArgs renamed &&
            string.Equals(renamed.OldFullPath, file.FullName, StringComparison.OrdinalIgnoreCase))
        {
            // Rolled by rename: read what is left of the file under its new name before starting over
            Volatile.Write(ref renamedToPath, renamed.FullPath);
        }

        Interlocked.Exchange(ref isResetRequested, 1);
        RequestRead();
    }

    private void OnWatcherError(
        object sender,
        ErrorEventArgs e)
        => RequestRead();

    private void RequestRead()
    {
        if (!isRunning || isPaused)
        {
            return;
        }

        Interlocked.Exchange(ref isReadRequested, 1);

        // Only one read at a time; a request that arrives during a read makes that read run once more.
        while (Volatile.Read(ref isReadRequested) == 1 &&
               Interlocked.CompareExchange(ref isReading, 1, 0) == 0)
        {
            try
            {
                while (isRunning &&
                       !isPaused &&
                       Interlocked.Exchange(ref isReadRequested, 0) == 1)
                {
                    ReadNewLines();
                }
            }
            finally
            {
                Volatile.Write(ref isReading, 0);
            }
        }
    }

    private void ReadNewLines()
    {
        try
        {
            if (Interlocked.Exchange(ref isResetRequested, 0) == 1)
            {
                var renamedTo = Interlocked.Exchange(ref renamedToPath, null);
                if (renamedTo is not null)
                {
                    ReadToEnd(renamedTo, isTailedPath: false);
                    RaisePartialLine();
                }

                ResetToStartOfFile();
            }

            ReadToEnd(file.FullName, isTailedPath: true);
        }
        catch (IOException)
        {
            // The file is temporarily inaccessible; the next notification or poll retries.
        }
        catch (UnauthorizedAccessException)
        {
            // The file is temporarily inaccessible; the next notification or poll retries.
        }
    }

    private void ReadToEnd(
        string path,
        bool isTailedPath)
    {
        if (!File.Exists(path))
        {
            return;
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1);

        if (isTailedPath && !IsSameFile(stream))
        {
            if (!IsReplacedFileToBeReadFromStart())
            {
                return;
            }

            ResetToStartOfFile();
            IsSameFile(stream);
        }
        else
        {
            replacedSinceTicks = -1;
        }

        if (stream.Length == offset)
        {
            RaisePartialLineWhenQuiet();
            return;
        }

        stream.Seek(offset, SeekOrigin.Begin);

        int bytesRead;
        while (isRunning &&
               (bytesRead = stream.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            var skip = GetPreambleLength(bytesRead);
            ProcessBytes(readBuffer.AsSpan(skip, bytesRead - skip));
            offset += bytesRead;
            lastGrowthTicks = Environment.TickCount64;
        }
    }

    /// <summary>
    /// Gets whether the file at the path is still the file being read: its first bytes are the same, and it is not shorter.
    /// The first time it is called after a reset, it remembers the first bytes.
    /// </summary>
    private bool IsSameFile(FileStream stream)
    {
        if (stream.Length < offset)
        {
            return false;
        }

        Span<byte> current = stackalloc byte[head.Length];
        var count = (int)System.Math.Min(stream.Length, head.Length);
        stream.Seek(0, SeekOrigin.Begin);
        stream.ReadExactly(current[..count]);

        var common = System.Math.Min(count, headCount);
        if (!current[..common].SequenceEqual(head.AsSpan(0, common)))
        {
            return false;
        }

        if (count > headCount)
        {
            current[headCount..count].CopyTo(head.AsSpan(headCount));
            headCount = count;
        }

        return true;
    }

    /// <summary>
    /// Another file is at the path. A rename or delete notification reads the rest of the old file first,
    /// so wait for it; without one in a moment, the file was truncated or copied over, and is read from the start.
    /// </summary>
    private bool IsReplacedFileToBeReadFromStart()
    {
        if (Volatile.Read(ref isResetRequested) == 1)
        {
            return false;
        }

        var now = Environment.TickCount64;
        if (replacedSinceTicks < 0)
        {
            replacedSinceTicks = now;
            return false;
        }

        if (now - replacedSinceTicks < (long)ReplacedFileGracePeriod.TotalMilliseconds)
        {
            return false;
        }

        replacedSinceTicks = -1;
        return true;
    }

    private void RememberHead()
    {
        headCount = 0;
        try
        {
            if (!File.Exists(file.FullName))
            {
                return;
            }

            using var stream = new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1);

            IsSameFile(stream);
        }
        catch (IOException)
        {
            // Remembered on the first read instead
        }
        catch (UnauthorizedAccessException)
        {
            // Remembered on the first read instead
        }
    }

    private void RaisePartialLineWhenQuiet()
    {
        // Only a line that was there before tailing started; one written since waits for its line break
        if (partialLine.WrittenCount > 0 &&
            offset - partialLine.WrittenCount < lengthAtStart &&
            Environment.TickCount64 - lastGrowthTicks >= (long)PartialLineQuietPeriod.TotalMilliseconds)
        {
            RaisePartialLine();
        }
    }

    /// <summary>
    /// Raises a last line that has no line break, at the end of a file that won't grow any more.
    /// </summary>
    private void RaisePartialLine()
    {
        if (partialLine.WrittenCount > 0)
        {
            RaiseLine(ReadOnlySpan<byte>.Empty);
        }
    }

    private int GetPreambleLength(int bytesRead)
        => offset == 0 &&
           bytesRead >= Utf8Preamble.Length &&
           readBuffer.AsSpan(0, Utf8Preamble.Length).SequenceEqual(Utf8Preamble)
            ? Utf8Preamble.Length
            : 0;

    private void ProcessBytes(ReadOnlySpan<byte> bytes)
    {
        while (!bytes.IsEmpty)
        {
            var newLineIndex = bytes.IndexOf((byte)'\n');
            if (newLineIndex < 0)
            {
                partialLine.Write(bytes);
                return;
            }

            RaiseLine(bytes[..newLineIndex]);
            bytes = bytes[(newLineIndex + 1)..];
        }
    }

    private void RaiseLine(ReadOnlySpan<byte> lineBytes)
    {
        string line;
        if (partialLine.WrittenCount == 0)
        {
            line = Encoding.UTF8.GetString(lineBytes);
        }
        else
        {
            partialLine.Write(lineBytes);
            line = Encoding.UTF8.GetString(partialLine.WrittenSpan);
            partialLine.ResetWrittenCount();
        }

        line = line.TrimEnd('\r');
        numberOfLines++;

        if (line.Length > 0)
        {
            LineAdded?.Invoke(this, new TailLineEventArgs(new TailLine(file, numberOfLines, line)));
        }
    }

    private void ResetToStartOfFile()
    {
        offset = 0;
        lengthAtStart = 0;
        numberOfLines = 0;
        headCount = 0;
        replacedSinceTicks = -1;
        partialLine.ResetWrittenCount();
    }
}