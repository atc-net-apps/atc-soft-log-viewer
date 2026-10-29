namespace Atc.LogFiles.Collector;

public interface IFileCollector
{
    event EventHandler<LogEntryEventArgs>? CollectedEntry;

    event EventHandler<FileEventArgs>? CollectedFileDone;

    event EventHandler<FilesEventArgs>? CollectedFilesDone;

    /// <summary>
    /// Occurs for a line that looks like the start of an entry but is not in this collector's format.
    /// </summary>
    event EventHandler<LineNotReadEventArgs>? LineNotRead;

    bool CanParseFileFormat(FileInfo file);

    Task CollectFile(
        FileInfo file,
        LogFileCollectorConfiguration config,
        bool useMonitoring,
        CancellationToken cancellationToken);

    Task CollectFolder(
        DirectoryInfo directory,
        LogFileCollectorConfiguration config,
        bool useMonitoring,
        CancellationToken cancellationToken);

    void MonitorFileIfNeeded(
        FileInfo fileInfo,
        long lastLineNumber = 0);

    void StopMonitoringAllFiles();

    void StopMonitoringFile(FileInfo file);
}