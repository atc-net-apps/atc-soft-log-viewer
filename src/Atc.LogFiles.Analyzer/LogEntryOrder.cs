namespace Atc.LogFiles.Analyzer;

/// <summary>
/// The order entries are shown in: by time stamp, then by file and line, so entries with the same
/// time stamp always come in the order they were written.
/// </summary>
internal sealed class LogEntryOrder : IComparer<AtcLogEntry>
{
    public static readonly LogEntryOrder Instance = new();

    public int Compare(
        AtcLogEntry? x,
        AtcLogEntry? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var result = x.TimeStamp.CompareTo(y.TimeStamp);
        if (result != 0)
        {
            return result;
        }

        result = string.CompareOrdinal(x.SourceIdentifier, y.SourceIdentifier);
        return result != 0
            ? result
            : x.LineNumber.CompareTo(y.LineNumber);
    }
}