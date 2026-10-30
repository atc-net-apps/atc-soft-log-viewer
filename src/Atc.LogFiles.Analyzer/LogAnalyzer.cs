// ReSharper disable ConvertIfStatementToReturnStatement
// ReSharper disable InvertIf
namespace Atc.LogFiles.Analyzer;

public sealed class LogAnalyzer : ILogAnalyzer, IDisposable
{
    /// <summary>
    /// How long live (tailed) entries are buffered before <see cref="CollectedEntries"/> is raised.
    /// </summary>
    private static readonly TimeSpan LiveBatchInterval = TimeSpan.FromMilliseconds(100);

    private readonly ILog4NetFileCollector log4NetFileCollector;
    private readonly INLogFileCollector nlogFileCollector;
    private readonly ISerilogFileCollector serilogFileCollector;
    private readonly Lock syncLock = new();
    private readonly Lock raiseLock = new();
    private readonly ConcurrentDictionary<string, bool> newFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> knownFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AtcLogEntry> logEntries = [];
    private readonly List<AtcLogEntry> pendingEntries = [];
    private readonly long[] logLevelCounts = new long[(int)LogLevel.None + 1];
    private readonly HashSet<string> sourceSystems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LinesNotRead> linesNotRead = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileNotRead> filesNotRead = new(StringComparer.OrdinalIgnoreCase);
    private readonly Timer flushTimer;
    private bool isFlushScheduled;

    /// <summary>
    /// Whether <see cref="logEntries"/> is in <see cref="LogEntryOrder"/>. Live entries mostly arrive in
    /// order; files read in parallel don't, and are sorted once, when a filter needs them.
    /// </summary>
    private bool isLogEntriesInOrder = true;
    private bool isNotReadChanged;
    private long linesNotReadCount;
    private long filterVersion;
    private long sourceSystemsVersion;
    private int activeCollectCount;
    private FileSystemWatcher? fileSystemWatcher;
    private DirectoryInfo? watchedDirectory;
    private LogFilter logFilter;
    private LogFileCollectorType activeLogFileCollectorType;
    private LogFileCollectorConfiguration? activeLogFileCollectorConfiguration;

    public LogAnalyzer(
        ILog4NetFileCollector log4NetFileCollector,
        INLogFileCollector nlogFileCollector,
        ISerilogFileCollector serilogFileCollector)
    {
        ArgumentNullException.ThrowIfNull(log4NetFileCollector);
        ArgumentNullException.ThrowIfNull(nlogFileCollector);
        ArgumentNullException.ThrowIfNull(serilogFileCollector);

        this.log4NetFileCollector = log4NetFileCollector;
        this.nlogFileCollector = nlogFileCollector;
        this.serilogFileCollector = serilogFileCollector;

        logFilter = new LogFilter(
            LogLevelTrace: true,
            LogLevelDebug: true,
            LogLevelInfo: true,
            LogLevelWarning: true,
            LogLevelError: true,
            LogLevelCritical: true,
            IncludeText: string.Empty,
            DateTimeFrom: null,
            DateTimeTo: null,
            SourceSystem: string.Empty);

        flushTimer = new Timer(_ => NotifyAndClearBuffer(raiseWhenEmpty: false));

        this.log4NetFileCollector.LineNotRead += OnLineNotRead;
        this.nlogFileCollector.LineNotRead += OnLineNotRead;
        this.serilogFileCollector.LineNotRead += OnLineNotRead;

        this.log4NetFileCollector.CollectedEntry += OnCollectedEntry;
        this.log4NetFileCollector.CollectedFilesDone += OnCollectedFilesDone;

        this.nlogFileCollector.CollectedEntry += OnCollectedEntry;
        this.nlogFileCollector.CollectedFilesDone += OnCollectedFilesDone;

        this.serilogFileCollector.CollectedEntry += OnCollectedEntry;
        this.serilogFileCollector.CollectedFilesDone += OnCollectedFilesDone;
    }

    public event EventHandler<LogEntryBatchEventArgs>? CollectedEntries;

    public event EventHandler? NotReadChanged;

    public LogFileCollectorType DetermineCollectorType(DirectoryInfo directory)
    {
        ArgumentNullException.ThrowIfNull(directory);

        var files = Directory
            .GetFiles(directory.FullName)
            .Select(x => new FileInfo(x))
            .OrderByDescending(x => x.CreationTime);

        return files
            .Select(DetermineCollectorType)
            .FirstOrDefault(x => x != LogFileCollectorType.None);
    }

    public LogFileCollectorType DetermineCollectorType(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (serilogFileCollector.CanParseFileFormat(file))
        {
            return LogFileCollectorType.Serilog;
        }

        if (nlogFileCollector.CanParseFileFormat(file))
        {
            return LogFileCollectorType.NLog;
        }

        if (log4NetFileCollector.CanParseFileFormat(file))
        {
            return LogFileCollectorType.Log4Net;
        }

        return LogFileCollectorType.None;
    }

    public async Task CollectFile(
        LogFileCollectorType logFileCollectorType,
        FileInfo file,
        LogFileCollectorConfiguration config,
        bool useMonitoring,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(config);

        var collector = GetCollector(logFileCollectorType);
        RememberExistingFiles(file.Directory!);

        await CollectAndNotify(
                logFileCollectorType,
                config,
                () => collector.CollectFile(
                    file,
                    config,
                    useMonitoring,
                    cancellationToken))
            .ConfigureAwait(false);

        StartWatchingForNewFiles(file.Directory!);
    }

    public async Task CollectFolder(
        LogFileCollectorType logFileCollectorType,
        DirectoryInfo directory,
        LogFileCollectorConfiguration config,
        bool useMonitoring,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(config);

        var collector = GetCollector(logFileCollectorType);
        RememberExistingFiles(directory);

        await CollectAndNotify(
                logFileCollectorType,
                config,
                () => collector.CollectFolder(
                    directory,
                    config,
                    useMonitoring,
                    cancellationToken))
            .ConfigureAwait(false);

        StartWatchingForNewFiles(directory);
    }

    public void SetFilter(LogFilter filter)
        => ApplyFilter(filter);

    public LogEntryBatch ApplyFilter(LogFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        AtcLogEntry[] snapshot;
        long version;
        lock (syncLock)
        {
            logFilter = filter;
            version = ++filterVersion;
            pendingEntries.Clear();
            snapshot = SnapshotInOrder();
        }

        var entries = FindMatches(snapshot, filter);

        return new LogEntryBatch(entries, version);
    }

    public long SourceSystemsVersion
        => Interlocked.Read(ref sourceSystemsVersion);

    public IDictionary<string, string> GetSourceSystems()
    {
        List<string> systems;
        lock (syncLock)
        {
            systems = [.. sourceSystems];
        }

        systems.Sort(StringComparer.Ordinal);
        systems = DropDownFirstItemTypeHelper.EnsureFirstItemType(systems, DropDownFirstItemType.Blank);

        return systems.ToDictionary(
            x => x,
            x => x,
            StringComparer.Ordinal);
    }

    public AtcLogEntry[] GetFilteredLogEntries()
    {
        AtcLogEntry[] snapshot;
        lock (syncLock)
        {
            snapshot = SnapshotInOrder();
        }

        var filter = logFilter;
        return FindMatches(snapshot, filter);
    }

    public LogStatistics GetLogStatistics()
    {
        lock (syncLock)
        {
            return new LogStatistics
            {
                Count = logEntries.Count,
                CriticalCount = logLevelCounts[(int)LogLevel.Critical],
                ErrorCount = logLevelCounts[(int)LogLevel.Error],
                WarningCount = logLevelCounts[(int)LogLevel.Warning],
                InformationCount = logLevelCounts[(int)LogLevel.Information],
                DebugCount = logLevelCounts[(int)LogLevel.Debug],
                TraceCount = logLevelCounts[(int)LogLevel.Trace],
                LinesNotReadCount = linesNotReadCount,
                FilesNotReadCount = filesNotRead.Count,
            };
        }
    }

    public NotReadSummary GetNotRead()
    {
        lock (syncLock)
        {
            return new NotReadSummary(
                [.. linesNotRead.Values.OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)],
                [.. filesNotRead.Values.OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)]);
        }
    }

    public void ClearLogEntries()
    {
        lock (syncLock)
        {
            linesNotRead.Clear();
            filesNotRead.Clear();
            linesNotReadCount = 0;
            logEntries.Clear();
            isLogEntriesInOrder = true;
            pendingEntries.Clear();
            sourceSystems.Clear();
            Interlocked.Increment(ref sourceSystemsVersion);
            Array.Clear(logLevelCounts);
        }
    }

    public void StopMonitoringAndClearLogEntries()
    {
        log4NetFileCollector.StopMonitoringAllFiles();
        nlogFileCollector.StopMonitoringAllFiles();
        serilogFileCollector.StopMonitoringAllFiles();
        ClearLogEntries();
    }

    public void Dispose()
    {
        flushTimer.Dispose();
        fileSystemWatcher?.Dispose();
    }

    /// <summary>
    /// Copies the entries in order, sorting them first if needed. Call it inside <see cref="syncLock"/>.
    /// </summary>
    private AtcLogEntry[] SnapshotInOrder()
    {
        if (!isLogEntriesInOrder)
        {
            logEntries.Sort(LogEntryOrder.Instance);
            isLogEntriesInOrder = true;
        }

        return [.. logEntries];
    }

    private static AtcLogEntry[] FindMatches(
        AtcLogEntry[] entries,
        LogFilter filter)
    {
        // Matches are written over the front of the snapshot, which is a copy only this call uses
        var count = 0;
        foreach (var entry in entries)
        {
            if (IsMatch(entry, filter))
            {
                entries[count++] = entry;
            }
        }

        return count == entries.Length
            ? entries
            : entries[..count];
    }

    private static bool IsMatch(
        AtcLogEntry entry,
        LogFilter filter)
        => IsLogLevelEnabled(entry.LogLevel, filter) &&
           (!filter.DateTimeFrom.HasValue || entry.TimeStamp >= filter.DateTimeFrom.Value) &&
           (!filter.DateTimeTo.HasValue || entry.TimeStamp <= filter.DateTimeTo.Value) &&
           (filter.IncludeText.Length == 0 ||
            entry.MessageFull.Contains(filter.IncludeText, StringComparison.OrdinalIgnoreCase)) &&
           (filter.SourceSystem.Length == 0 ||
            entry.SourceSystem.Equals(filter.SourceSystem, StringComparison.Ordinal));

    private static bool IsLogLevelEnabled(
        LogLevel logLevel,
        LogFilter filter)
        => logLevel switch
        {
            LogLevel.Trace => filter.LogLevelTrace,
            LogLevel.Debug => filter.LogLevelDebug,
            LogLevel.Information => filter.LogLevelInfo,
            LogLevel.Warning => filter.LogLevelWarning,
            LogLevel.Error => filter.LogLevelError,
            LogLevel.Critical => filter.LogLevelCritical,
            _ => true,
        };

    private IFileCollector GetCollector(
        LogFileCollectorType logFileCollectorType)
        => logFileCollectorType switch
        {
            LogFileCollectorType.Log4Net => log4NetFileCollector,
            LogFileCollectorType.NLog => nlogFileCollector,
            LogFileCollectorType.Serilog => serilogFileCollector,
            _ => throw new SwitchCaseDefaultException(logFileCollectorType),
        };

    /// <summary>
    /// Runs a collect while entries are buffered, and raises them as one batch when it is done.
    /// </summary>
    private async Task CollectAndNotify(
        LogFileCollectorType logFileCollectorType,
        LogFileCollectorConfiguration config,
        Func<Task> collect)
    {
        activeLogFileCollectorType = logFileCollectorType;
        activeLogFileCollectorConfiguration = config;

        Interlocked.Increment(ref activeCollectCount);
        try
        {
            await collect().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref activeCollectCount);
        }

        NotifyAndClearBuffer(raiseWhenEmpty: true);
    }

    private void StartWatchingForNewFiles(DirectoryInfo directory)
    {
        if (fileSystemWatcher is not null)
        {
            fileSystemWatcher.Created -= OnFileCreated;
            fileSystemWatcher.Changed -= OnFileChanged;
            fileSystemWatcher.Error -= OnFolderWatcherError;
            fileSystemWatcher.Dispose();
        }

        newFiles.Clear();
        watchedDirectory = directory;

        fileSystemWatcher = new FileSystemWatcher(directory.FullName)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 64 * 1024,
        };

        fileSystemWatcher.Created += OnFileCreated;
        fileSystemWatcher.Changed += OnFileChanged;
        fileSystemWatcher.Error += OnFolderWatcherError;
        fileSystemWatcher.EnableRaisingEvents = true;
    }

    internal FileSystemWatcher? FolderWatcher => fileSystemWatcher;

    /// <summary>
    /// The folder watcher lost events, for example when its buffer overflowed in a burst of new files.
    /// Looks for files it missed, so they are not lost without a sign.
    /// </summary>
    /// <param name="sender">The folder watcher.</param>
    /// <param name="e">The error.</param>
    internal void OnFolderWatcherError(
        object? sender,
        ErrorEventArgs e)
    {
        var config = activeLogFileCollectorConfiguration;
        var directory = watchedDirectory;
        if (config is null ||
            directory is null)
        {
            return;
        }

        string[] paths;
        try
        {
            paths = Directory.GetFiles(directory.FullName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The folder is gone or can't be read right now, for example a network share that dropped
            return;
        }

        foreach (var path in paths)
        {
            if (knownFiles.TryAdd(path, 0) &&
                MatchesFileNameTerms(Path.GetFileName(path), config))
            {
                newFiles.TryAdd(path, false);
                TryCollectNewFile(path, config);
            }
        }
    }

    /// <summary>
    /// Remembers the files in the folder before it is read, so a rescan after a watcher error only
    /// picks up files that are new.
    /// </summary>
    private void RememberExistingFiles(DirectoryInfo directory)
    {
        knownFiles.Clear();
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory.FullName))
            {
                knownFiles.TryAdd(path, 0);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Reading the folder fails with its own error
        }
    }

    private void OnCollectedEntry(
        object? sender,
        LogEntryEventArgs e)
    {
        var logEntry = e.Entry;
        lock (syncLock)
        {
            if (isLogEntriesInOrder &&
                logEntries.Count > 0 &&
                LogEntryOrder.Instance.Compare(logEntries[^1], logEntry) > 0)
            {
                isLogEntriesInOrder = false;
            }

            logEntries.Add(logEntry);
            logLevelCounts[(int)logEntry.LogLevel]++;
            if (sourceSystems.Add(logEntry.SourceSystem))
            {
                Interlocked.Increment(ref sourceSystemsVersion);
            }

            if (!IsMatch(logEntry, logFilter))
            {
                return;
            }

            pendingEntries.Add(logEntry);

            if (Volatile.Read(ref activeCollectCount) == 0 &&
                !isFlushScheduled)
            {
                isFlushScheduled = true;
                flushTimer.Change(LiveBatchInterval, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnLineNotRead(
        object? sender,
        LineNotReadEventArgs e)
    {
        // Only lines another format can read: a message line that starts with a digit, or a line the
        // logger is still writing, is not a line in another format
        var format = FindOtherFormat(sender, e.Line);
        if (format == LogFileCollectorType.None)
        {
            return;
        }

        lock (syncLock)
        {
            var filePath = e.File.FullName;
            linesNotRead[filePath] = linesNotRead.TryGetValue(filePath, out var known)
                ? known with { Count = known.Count + 1 }
                : new LinesNotRead(filePath, 1, e.LineNumber, e.Line, format);
            linesNotReadCount++;
            MarkNotReadChanged();
        }
    }

    private LogFileCollectorType FindOtherFormat(
        object? sender,
        string line)
    {
        if (!ReferenceEquals(sender, serilogFileCollector) && serilogFileCollector.IsRootLine(line))
        {
            return LogFileCollectorType.Serilog;
        }

        if (!ReferenceEquals(sender, nlogFileCollector) && nlogFileCollector.IsRootLine(line))
        {
            return LogFileCollectorType.NLog;
        }

        if (!ReferenceEquals(sender, log4NetFileCollector) && log4NetFileCollector.IsRootLine(line))
        {
            return LogFileCollectorType.Log4Net;
        }

        return LogFileCollectorType.None;
    }

    /// <summary>
    /// Raises <see cref="NotReadChanged"/> with the next batch. Call it inside <see cref="syncLock"/>.
    /// </summary>
    private void MarkNotReadChanged()
    {
        isNotReadChanged = true;
        if (Volatile.Read(ref activeCollectCount) == 0 &&
            !isFlushScheduled)
        {
            isFlushScheduled = true;
            flushTimer.Change(LiveBatchInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnCollectedFilesDone(
        object? sender,
        FilesEventArgs e)
    {
        if (Volatile.Read(ref activeCollectCount) == 0)
        {
            NotifyAndClearBuffer(raiseWhenEmpty: false);
        }
    }

    /// <summary>
    /// A new file in the watched folder. Loggers often create the file before they write the first line,
    /// so a file that can't be read as a log yet is tried again when it changes (<see cref="OnFileChanged"/>).
    /// </summary>
    private void OnFileCreated(
        object sender,
        FileSystemEventArgs e)
    {
        var config = activeLogFileCollectorConfiguration;
        if (config is null ||
            !MatchesFileNameTerms(e.Name, config))
        {
            return;
        }

        knownFiles.TryAdd(e.FullPath, 0);
        newFiles.TryAdd(e.FullPath, false);
        TryCollectNewFile(e.FullPath, config);
    }

    private void OnFileChanged(
        object sender,
        FileSystemEventArgs e)
    {
        var config = activeLogFileCollectorConfiguration;
        if (config is not null &&
            newFiles.ContainsKey(e.FullPath))
        {
            TryCollectNewFile(e.FullPath, config);
        }
    }

    private static bool MatchesFileNameTerms(
        string? fileName,
        LogFileCollectorConfiguration config)
        => config.FileNameTerms.Count == 0 ||
           (fileName is not null &&
            config.FileNameTerms.Any(term => fileName.Contains(term, StringComparison.OrdinalIgnoreCase)));

    private void TryCollectNewFile(
        string fullPath,
        LogFileCollectorConfiguration config)
    {
        // The value is true while the file is being collected, so a burst of changes collects it once
        if (!newFiles.TryUpdate(fullPath, newValue: true, comparisonValue: false))
        {
            return;
        }

        var collectorType = activeLogFileCollectorType;
        var collector = GetCollector(collectorType);
        var file = new FileInfo(fullPath);

        if (!collector.CanParseFileFormat(file))
        {
            // The logger may have written its first line since the check, so the format can be ours by now
            var format = DetermineCollectorType(file);
            if (format == LogFileCollectorType.None)
            {
                // Still empty or without a complete line; the next change tries again
                newFiles.TryUpdate(fullPath, newValue: false, comparisonValue: true);
                return;
            }

            if (format != collectorType)
            {
                // A log file in another format: it is not read, but it is not silently ignored either
                newFiles.TryRemove(fullPath, out _);
                lock (syncLock)
                {
                    filesNotRead[fullPath] = new FileNotRead(fullPath, format);
                    MarkNotReadChanged();
                }

                return;
            }
        }

        switch (collector.IsCopyOfReadFile(file))
        {
            case null:
                // The first line isn't complete yet; the next change tries again
                newFiles.TryUpdate(fullPath, newValue: false, comparisonValue: true);
                return;
            case true:
                // An archive copy from a roll by copy and truncate: its lines have been read already
                newFiles.TryRemove(fullPath, out _);
                return;
        }

        newFiles.TryRemove(fullPath, out _);

        TaskHelper.FireAndForget(
            CollectAndNotify(
                collectorType,
                config,
                () => collector.CollectFile(file, config, useMonitoring: true, CancellationToken.None)));
    }

    /// <summary>
    /// Raises the buffered entries. Batches are raised one at a time and in order, from whichever thread
    /// flushes; collecting goes on meanwhile, because it only needs <see cref="syncLock"/>.
    /// </summary>
    private void NotifyAndClearBuffer(bool raiseWhenEmpty)
    {
        lock (raiseLock)
        {
            AtcLogEntry[] entries;
            long version;
            bool notReadChanged;
            lock (syncLock)
            {
                entries = [.. pendingEntries];
                pendingEntries.Clear();
                isFlushScheduled = false;
                version = filterVersion;
                notReadChanged = isNotReadChanged;
                isNotReadChanged = false;
            }

            if (entries.Length > 0 || raiseWhenEmpty)
            {
                Array.Sort(entries, LogEntryOrder.Instance);
                CollectedEntries?.Invoke(this, new LogEntryBatchEventArgs(new LogEntryBatch(entries, version)));
            }

            if (notReadChanged)
            {
                NotReadChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}