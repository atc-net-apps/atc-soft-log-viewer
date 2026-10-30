namespace Atc.LogFiles.Benchmarks;

/// <summary>
/// Collector stand-in that lets a benchmark push entries straight into a <see cref="Analyzer.LogAnalyzer"/>.
/// </summary>
internal sealed class FakeFileCollector : ILog4NetFileCollector, INLogFileCollector, ISerilogFileCollector
{
    public event EventHandler<LogEntryEventArgs>? CollectedEntry;

    public event EventHandler<FileEventArgs>? CollectedFileDone;

    public event EventHandler<FilesEventArgs>? CollectedFilesDone;

    public event EventHandler<LineNotReadEventArgs>? LineNotRead;

    public void Raise(AtcLogEntry entry)
        => CollectedEntry?.Invoke(this, new LogEntryEventArgs(entry));

    public bool IsRootLine(string line)
        => false;

    public void RaiseLineNotRead(string line)
        => LineNotRead?.Invoke(this, new LineNotReadEventArgs(new FileInfo("fake.log"), 1, line));

    public void RaiseFilesDone()
    {
        CollectedFileDone?.Invoke(this, new FileEventArgs(new FileInfo("fake.log")));
        CollectedFilesDone?.Invoke(this, new FilesEventArgs([]));
    }

    public bool CanParseFileFormat(FileInfo file)
        => false;

    public Task CollectFile(
        FileInfo file,
        LogFileCollectorConfiguration config,
        bool useMonitoring,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task CollectFolder(
        DirectoryInfo directory,
        LogFileCollectorConfiguration config,
        bool useMonitoring,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public void MonitorFileIfNeeded(
        FileInfo fileInfo,
        long lastLineNumber = 0)
    {
    }

    public void StopMonitoringAllFiles()
    {
    }

    public void StopMonitoringFile(FileInfo file)
    {
    }
}