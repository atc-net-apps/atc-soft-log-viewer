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
    private readonly List<AtcLogEntry> logEntries = [];
    private readonly List<AtcLogEntry> pendingEntries = [];
    private readonly long[] logLevelCounts = new long[(int)LogLevel.None + 1];
    private readonly HashSet<string> sourceSystems = new(StringComparer.Ordinal);
    private readonly Timer flushTimer;
    private bool isFlushScheduled;
    private long filterVersion;
    private int activeCollectCount;
    private FileSystemWatcher? fileSystemWatcher;
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

        this.log4NetFileCollector.CollectedEntry += OnCollectedEntry;
        this.log4NetFileCollector.CollectedFilesDone += OnCollectedFilesDone;

        this.nlogFileCollector.CollectedEntry += OnCollectedEntry;
        this.nlogFileCollector.CollectedFilesDone += OnCollectedFilesDone;

        this.serilogFileCollector.CollectedEntry += OnCollectedEntry;
        this.serilogFileCollector.CollectedFilesDone += OnCollectedFilesDone;
    }

    public event EventHandler<LogEntryBatchEventArgs>? CollectedEntries;

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
            snapshot = [.. logEntries];
        }

        var entries = snapshot
            .Where(entry => IsMatch(entry, filter))
            .OrderBy(x => x.TimeStamp)
            .ToArray();

        return new LogEntryBatch(entries, version);
    }

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
            snapshot = [.. logEntries];
        }

        var filter = logFilter;
        return snapshot
            .Where(entry => IsMatch(entry, filter))
            .OrderBy(x => x.TimeStamp)
            .ToArray();
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
            };
        }
    }

    public void ClearLogEntries()
    {
        lock (syncLock)
        {
            logEntries.Clear();
            pendingEntries.Clear();
            sourceSystems.Clear();
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
            fileSystemWatcher.Dispose();
        }

        fileSystemWatcher = new FileSystemWatcher(directory.FullName) { EnableRaisingEvents = true, };
        fileSystemWatcher.Created += OnFileCreated;
    }

    private void OnCollectedEntry(
        object? sender,
        LogEntryEventArgs e)
    {
        var logEntry = e.Entry;
        lock (syncLock)
        {
            logEntries.Add(logEntry);
            logLevelCounts[(int)logEntry.LogLevel]++;
            sourceSystems.Add(logEntry.SourceSystem);

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

    private void OnCollectedFilesDone(
        object? sender,
        FilesEventArgs e)
    {
        if (Volatile.Read(ref activeCollectCount) == 0)
        {
            NotifyAndClearBuffer(raiseWhenEmpty: false);
        }
    }

    private void OnFileCreated(
        object sender,
        FileSystemEventArgs e)
    {
        if (activeLogFileCollectorConfiguration is null)
        {
            return;
        }

        var fileInfo = new FileInfo(e.FullPath);

        TaskHelper.FireAndForget(
            CollectFile(
                activeLogFileCollectorType,
                fileInfo,
                activeLogFileCollectorConfiguration,
                useMonitoring: true,
                CancellationToken.None));
    }

    private void NotifyAndClearBuffer(bool raiseWhenEmpty)
    {
        AtcLogEntry[] entries;
        long version;
        lock (syncLock)
        {
            entries = [.. pendingEntries];
            pendingEntries.Clear();
            isFlushScheduled = false;
            version = filterVersion;
        }

        if (entries.Length == 0 && !raiseWhenEmpty)
        {
            return;
        }

        Array.Sort(entries, static (x, y) => x.TimeStamp.CompareTo(y.TimeStamp));
        CollectedEntries?.Invoke(this, new LogEntryBatchEventArgs(new LogEntryBatch(entries, version)));
    }
}