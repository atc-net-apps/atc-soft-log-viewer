namespace Atc.LogFiles.Collector;

/// <summary>
/// Collects log entries from files of one log format, and optionally keeps monitoring them for new entries.
/// </summary>
public abstract class LogFileCollectorBase : LogCollectorBase, IFileCollector
{
    /// <summary>
    /// How long a monitored file must be quiet before its last entry is raised,
    /// since lines that belong to the entry (e.g. a stack trace) may still be on their way.
    /// </summary>
    private static readonly TimeSpan LastEntryQuietPeriod = TimeSpan.FromMilliseconds(300);

    private readonly ConcurrentDictionary<string, MonitoredFile> monitoredFiles = new(StringComparer.Ordinal);

    public event EventHandler<LogEntryEventArgs>? CollectedEntry;

    public event EventHandler<FileEventArgs>? CollectedFileDone;

    public event EventHandler<FilesEventArgs>? CollectedFilesDone;

    /// <summary>
    /// The file extensions (without dot) this log format is written to.
    /// </summary>
    protected abstract IReadOnlyCollection<string> DefaultLogExtensions { get; }

    public bool CanParseFileFormat(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (!DefaultLogExtensions.Contains(
                file.Extension.TrimStart('.'),
                StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var reader = OpenReader(file);
            for (var i = 0; i < 3; i++)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    return false;
                }

                if (ParseRootLine(string.Empty, string.Empty, 0, line) is not null)
                {
                    return true;
                }
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return false;
    }

    public async Task CollectFile(
        FileInfo file,
        LogFileCollectorConfiguration config,
        bool useMonitoring,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(config);

        if (IsFileMonitoring(file) ||
            !CanParseFileFormat(file))
        {
            return;
        }

        var collected = await CollectSingleFile(
                file,
                useMonitoring,
                cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (collected)
        {
            CollectedFilesDone?.Invoke(this, new FilesEventArgs([file]));
        }
    }

    public async Task CollectFolder(
        DirectoryInfo directory,
        LogFileCollectorConfiguration config,
        bool useMonitoring,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(config);

        var files = GetFiles(directory, config, DefaultLogExtensions);
        if (files.Count == 0)
        {
            return;
        }

        StopMonitoringAllFiles();

        var options = new ParallelOptions { CancellationToken = cancellationToken };

        await Parallel.ForEachAsync(
                files,
                options,
                async (file, token) =>
                {
                    if (CanParseFileFormat(file))
                    {
                        await CollectSingleFile(file, useMonitoring, token)
                            .ConfigureAwait(continueOnCapturedContext: false);
                    }
                })
            .ConfigureAwait(continueOnCapturedContext: false);

        CollectedFilesDone?.Invoke(this, new FilesEventArgs([.. files]));
    }

    public void MonitorFileIfNeeded(
        FileInfo fileInfo,
        long lastLineNumber = 0)
    {
        ArgumentNullException.ThrowIfNull(fileInfo);

        if (!fileInfo.IsCreatedToday() ||
            monitoredFiles.ContainsKey(fileInfo.FullName))
        {
            return;
        }

        var monitoredFile = new MonitoredFile(
            fileInfo,
            lastLineNumber,
            CreateAssembler(fileInfo, entry => CollectedEntry?.Invoke(this, new LogEntryEventArgs(entry))));

        if (monitoredFiles.TryAdd(fileInfo.FullName, monitoredFile))
        {
            monitoredFile.Start();
        }
        else
        {
            monitoredFile.Dispose();
        }
    }

    public bool IsFileMonitoring(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return monitoredFiles.ContainsKey(file.FullName);
    }

    public void StopMonitoringFile(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (monitoredFiles.TryRemove(file.FullName, out var monitoredFile))
        {
            monitoredFile.Dispose();
        }
    }

    public void StopMonitoringAllFiles()
    {
        foreach (var key in monitoredFiles.Keys)
        {
            if (monitoredFiles.TryRemove(key, out var monitoredFile))
            {
                monitoredFile.Dispose();
            }
        }
    }

    /// <summary>
    /// Reads all lines of the file as a stream, and raises <see cref="CollectedEntry"/> for each entry.
    /// </summary>
    /// <param name="file">The log file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether any entry was found, and the number of lines in the file.</returns>
    internal async Task<(bool IsSuccessFul, long LastLineNumber)> ReadAndParseLines(
        FileInfo file,
        CancellationToken cancellationToken)
    {
        var hasAnyEntries = false;
        var assembler = CreateAssembler(
            file,
            entry =>
            {
                hasAnyEntries = true;
                CollectedEntry?.Invoke(this, new LogEntryEventArgs(entry));
            });

        long lineNumber = 0;
        using (var reader = OpenReader(file))
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false) is { } line)
            {
                lineNumber++;
                assembler.AddLine(lineNumber, line);
            }
        }

        assembler.Flush();

        return (hasAnyEntries, lineNumber);
    }

    internal static IList<FileInfo> GetFiles(
        DirectoryInfo directory,
        LogFileCollectorConfiguration config,
        IEnumerable<string> logExtensions)
    {
        var createdStartDate = config.MaxDaysBack == 0
            ? DateTime.Now.AddDays(-365)
            : DateTime.Now.AddDays(-config.MaxDaysBack);

        var files = new List<FileInfo>();

        foreach (var extension in logExtensions)
        {
            var searchPattern = $"*.{extension.TrimStart('.')}";
            if (config.FileNameTerms.Count == 0)
            {
                files.AddRange(directory.GetFiles(searchPattern));
            }
            else
            {
                var filesByExtension = directory.GetFiles(searchPattern);
                foreach (var file in filesByExtension)
                {
                    foreach (var term in config.FileNameTerms)
                    {
                        if (file.Name.Contains(term, StringComparison.OrdinalIgnoreCase) &&
                            !files.Contains(file))
                        {
                            files.Add(file);
                        }
                    }
                }
            }
        }

        return files
            .Where(f => f.CreationTime >= createdStartDate && f.CreationTime <= DateTime.Now)
            .OrderBy(x => x.CreationTime)
            .ToList();
    }

    /// <summary>
    /// Parses a root line (a line with time stamp and level) of this log format.
    /// </summary>
    /// <param name="sourceIdentifier">The file name without extension.</param>
    /// <param name="sourceSystem">The source identifier without its date part.</param>
    /// <param name="lineNumber">The 1-based line number.</param>
    /// <param name="line">The line.</param>
    /// <returns>The entry, or <see langword="null"/> when the line is not a root line.</returns>
    protected abstract AtcLogEntry? ParseRootLine(
        string sourceIdentifier,
        string sourceSystem,
        long lineNumber,
        string line);

    private static StreamReader OpenReader(FileInfo file)
        => new(
            new FileStream(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan | FileOptions.Asynchronous),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

    private async Task<bool> CollectSingleFile(
        FileInfo file,
        bool useMonitoring,
        CancellationToken cancellationToken)
    {
        var (isSuccessFul, lastLineNumber) = await ReadAndParseLines(
                file,
                cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (!isSuccessFul)
        {
            return false;
        }

        CollectedFileDone?.Invoke(this, new FileEventArgs(file));

        if (useMonitoring)
        {
            MonitorFileIfNeeded(file, lastLineNumber);
        }

        return true;
    }

    private LogEntryAssembler CreateAssembler(
        FileInfo file,
        Action<AtcLogEntry> onEntry)
    {
        var sourceIdentifier = Path.GetFileNameWithoutExtension(file.FullName);
        var sourceSystem = GetSourceSystemFromSourceIdentifier(sourceIdentifier);

        return new LogEntryAssembler(
            (lineNumber, line) => ParseRootLine(sourceIdentifier, sourceSystem, lineNumber, line),
            onEntry);
    }

    /// <summary>
    /// A tailed file and the entry that is still being built from its lines.
    /// </summary>
    private sealed class MonitoredFile : IDisposable
    {
        private readonly Lock syncLock = new();
        private readonly TailFile tailFile;
        private readonly LogEntryAssembler assembler;
        private readonly Timer flushTimer;

        public MonitoredFile(
            FileInfo file,
            long lastLineNumber,
            LogEntryAssembler assembler)
        {
            this.assembler = assembler;
            tailFile = new TailFile(file, lastLineNumber);
            tailFile.LineAdded += OnLineAdded;
            flushTimer = new Timer(_ => Flush());
        }

        public void Start()
            => tailFile.Start();

        public void Dispose()
        {
            tailFile.LineAdded -= OnLineAdded;
            tailFile.Dispose();
            flushTimer.Dispose();
        }

        private void OnLineAdded(
            object? sender,
            TailLineEventArgs e)
        {
            var tailLine = e.Line;
            lock (syncLock)
            {
                assembler.AddLine(tailLine.LineNumber, tailLine.Line);
            }

            flushTimer.Change(LastEntryQuietPeriod, Timeout.InfiniteTimeSpan);
        }

        private void Flush()
        {
            lock (syncLock)
            {
                assembler.Flush();
            }
        }
    }
}