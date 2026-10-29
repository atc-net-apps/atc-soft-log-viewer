namespace Atc.LogAnalyzer;

public interface ILogAnalyzer
{
    /// <summary>
    /// Raised with entries that match the filter: once when a collect is done, and in batches for live entries.
    /// </summary>
    event Action<LogEntryBatch>? CollectedEntries;

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

    void ClearLogEntries();

    void StopMonitoringAndClearLogEntries();
}