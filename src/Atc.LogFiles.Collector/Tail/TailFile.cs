namespace Atc.LogFiles.Collector.Tail;

/// <summary>
/// Raises <see cref="LineAdded"/> for each complete line appended to a file.
/// </summary>
/// <remarks>
/// File system notifications and a slow backup poll both only request a read; at most one read runs at a time,
/// and it returns when the new content has been read, so an idle tailed file does not hold a thread.
/// A line is raised when its line break has been written, and a truncated or replaced file is read from the start.
/// </remarks>
public class TailFile : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly byte[] Utf8Preamble = Encoding.UTF8.GetPreamble();

    private readonly FileInfo file;
    private readonly FileSystemWatcher fileWatcher;
    private readonly Timer pollTimer;
    private readonly MemoryStream partialLine = new();
    private readonly byte[] readBuffer = new byte[64 * 1024];
    private long offset;
    private long numberOfLines;
    private int isReading;
    private int isReadRequested;
    private int isResetRequested;
    private volatile bool isRunning;
    private volatile bool isPaused;
    private bool isDisposed;

    public TailFile(
        FileInfo file,
        long lastLineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(file);

        this.file = file;
        numberOfLines = lastLineNumber;

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
        file.Refresh();
        offset = file.Exists
            ? file.Length
            : 0;

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
            partialLine.Dispose();
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
                ResetToStartOfFile();
            }

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

            if (stream.Length < offset)
            {
                // Truncated or replaced by a new file
                ResetToStartOfFile();
            }

            if (stream.Length == offset)
            {
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
            }
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
        if (partialLine.Length == 0)
        {
            line = Encoding.UTF8.GetString(lineBytes);
        }
        else
        {
            partialLine.Write(lineBytes);
            line = Encoding.UTF8.GetString(partialLine.GetBuffer(), 0, (int)partialLine.Length);
            partialLine.SetLength(0);
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
        numberOfLines = 0;
        partialLine.SetLength(0);
    }
}