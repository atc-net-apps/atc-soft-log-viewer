namespace Atc.LogFiles.Collector;

/// <summary>
/// A collected log entry.
/// </summary>
public sealed class LogEntryEventArgs : EventArgs
{
    public LogEntryEventArgs(AtcLogEntry entry)
        => Entry = entry;

    public AtcLogEntry Entry { get; }
}