namespace Atc.LogFiles.Collector.Tail;

/// <summary>
/// A complete line appended to a tailed file.
/// </summary>
public sealed class TailLineEventArgs : EventArgs
{
    public TailLineEventArgs(TailLine line)
        => Line = line;

    public TailLine Line { get; }
}