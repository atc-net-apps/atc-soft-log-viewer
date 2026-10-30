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

    /// <summary>
    /// Gets whether the line starts an entry in this collector's format.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns><see langword="true"/> when the line is a root line of this format.</returns>
    bool IsRootLine(string line);

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