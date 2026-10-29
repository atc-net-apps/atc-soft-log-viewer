namespace Atc.LogViewer.Benchmarks;

/// <summary>
/// Collector stand-in that lets a benchmark push entries straight into a <see cref="LogAnalyzer.LogAnalyzer"/>.
/// </summary>
internal sealed class FakeFileCollector : ILog4NetFileCollector, INLogFileCollector, ISerilogFileCollector
{
    public event Action<AtcLogEntry>? CollectedEntry;

    public event Action<FileInfo>? CollectedFileDone;

    public event Action<FileInfo[]>? CollectedFilesDone;

    public void Raise(AtcLogEntry entry)
        => CollectedEntry?.Invoke(entry);

    public void RaiseFilesDone()
    {
        CollectedFileDone?.Invoke(new FileInfo("fake.log"));
        CollectedFilesDone?.Invoke([]);
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