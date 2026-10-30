namespace Atc.LogFiles.Analyzer;

/// <summary>
/// The order entries are shown in: by time stamp, then by file and line, so entries with the same
/// time stamp always come in the order they were written.
/// </summary>
public sealed class LogEntryOrder : IComparer<AtcLogEntry>
{
    public static readonly LogEntryOrder Instance = new();

    /// <summary>
    /// Finds where an entry goes in a list in this order: after every entry that comes before it or ties
    /// with it, so a live entry from a slower file lands at its time, not at the end.
    /// </summary>
    /// <typeparam name="T">The list's rows.</typeparam>
    /// <param name="sorted">The list, in this order.</param>
    /// <param name="entry">The entry to place.</param>
    /// <param name="getEntry">Reads the entry of a row.</param>
    /// <returns>The index to insert at; the list's count when it goes at the end.</returns>
    public static int FindInsertIndex<T>(
        IReadOnlyList<T> sorted,
        AtcLogEntry entry,
        Func<T, AtcLogEntry> getEntry)
    {
        ArgumentNullException.ThrowIfNull(sorted);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(getEntry);

        var low = 0;
        var high = sorted.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (Instance.Compare(getEntry(sorted[middle]), entry) <= 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

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