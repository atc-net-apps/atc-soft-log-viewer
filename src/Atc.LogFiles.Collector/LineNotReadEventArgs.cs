namespace Atc.LogFiles.Collector;

/// <summary>
/// A line that looks like the start of a log entry (it starts with a digit) but is not in the
/// collector's format, so it was not read: for example a log4net line in a Serilog file.
/// </summary>
public sealed class LineNotReadEventArgs : EventArgs
{
    public LineNotReadEventArgs(
        FileInfo file,
        long lineNumber,
        string line)
    {
        File = file;
        LineNumber = lineNumber;
        Line = line;
    }

    public FileInfo File { get; }

    /// <summary>
    /// Gets the 1-based line number.
    /// </summary>
    public long LineNumber { get; }

    public string Line { get; }
}