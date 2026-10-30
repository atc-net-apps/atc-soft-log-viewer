namespace AtcSoft.Log.Viewer.Wpf.Models;

/// <summary>
/// A log entry as the list shows it: the analyzer's entry with its highlight colors. It wraps the entry
/// instead of copying it, which saves about half the memory of each row.
/// </summary>
/// <param name="Entry">The analyzer's entry.</param>
/// <param name="HighlightForeground">The text color.</param>
/// <param name="HighlightBackground">The background color.</param>
public sealed record AtcLogEntryEx(
    AtcLogEntry Entry,
    Brush HighlightForeground,
    Brush HighlightBackground)
{
    public string SourceIdentifier
        => Entry.SourceIdentifier;

    public string SourceSystem
        => Entry.SourceSystem;

    public long LineNumber
        => Entry.LineNumber;

    public DateTime TimeStamp
        => Entry.TimeStamp;

    public LogLevel LogLevel
        => Entry.LogLevel;

    public string MessageShort
        => Entry.MessageShort;

    public string MessageFull
        => Entry.MessageFull;
}