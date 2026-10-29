namespace Atc.LogFiles.Analyzer;

public interface ILogAnalyzer
{
    /// <summary>
    /// Raised with entries that match the filter: once when a collect is done, and in batches for live entries.
    /// </summary>
    event EventHandler<LogEntryBatchEventArgs>? CollectedEntries;

    /// <summary>
    /// Occurs when lines or files were found that are not read because they are in another format
    /// (at most once per batch interval). Read them with <see cref="GetNotRead"/>.
    /// </summary>
    event EventHandler? NotReadChanged;

    LogFileCollectorType DetermineCollectorType(DirectoryInfo directory);

    LogFileCollectorType DetermineCollectorType(FileInfo file);

    Task CollectFile(
        LogFileCollectorType logFileCollectorType,
        FileInfo file,
        LogFileCollectorConfiguration config,
        bool useMonitoring,
        CancellationToken cancellationToken);

    Task CollectFolder(
        LogFileCollectorType logFileCollectorType,
        DirectoryInfo directory,
        LogFileCollectorConfiguration config,
        bool useMonitoring,
        CancellationToken cancellationToken);

    public void SetFilter(LogFilter filter);

    /// <summary>
    /// Sets the filter and returns the matching entries in one step, so live entries are either in the
    /// result or delivered later by <see cref="CollectedEntries"/> with the returned filter version - not both.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>The matching entries and the new filter version.</returns>
    LogEntryBatch ApplyFilter(LogFilter filter);

    public IDictionary<string, string> GetSourceSystems();

    AtcLogEntry[] GetFilteredLogEntries();

    LogStatistics GetLogStatistics();

    /// <summary>
    /// Gets the lines and files that were not read because they are in another format.
    /// </summary>
    /// <returns>The summary.</returns>
    NotReadSummary GetNotRead();

    void ClearLogEntries();

    void StopMonitoringAndClearLogEntries();
}