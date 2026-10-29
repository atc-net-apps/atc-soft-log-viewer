namespace Atc.LogFiles.Analyzer;

/// <summary>
/// Log entries that match the filter, see <see cref="ILogAnalyzer.CollectedEntries"/>.
/// </summary>
public sealed class LogEntryBatchEventArgs : EventArgs
{
    public LogEntryBatchEventArgs(LogEntryBatch batch)
        => Batch = batch;

    public LogEntryBatch Batch { get; }
}