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

    /// <summary>
    /// Tells whether a new file is a copy of a file already read: it starts with the same first entry line.
    /// A logger that rolls by copy and truncate makes such a copy, and its lines have already been read.
    /// </summary>
    /// <param name="file">The new file.</param>
    /// <returns><see langword="null"/> while the file has no complete first line yet.</returns>
    bool? IsCopyOfReadFile(FileInfo file);

    void StopMonitoringFile(FileInfo file);
}