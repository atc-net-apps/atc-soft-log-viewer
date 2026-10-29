namespace Atc.LogCollector;

/// <summary>
/// Builds log entries from lines in file order.
/// </summary>
/// <remarks>
/// An entry starts with a root line (time stamp and level). The lines after it, up to the next root line,
/// belong to the entry - for example a stack trace or a JSON payload. A line that starts with a digit but is
/// not a root line ends the entry, and the lines after it are ignored until the next root line.
/// An entry is raised when the next root line arrives, or when <see cref="Flush"/> is called.
/// </remarks>
internal sealed class LogEntryAssembler
{
    internal const int MessageShortMaxLength = 160;
    internal const int MaxFollowingLines = 119;

    private readonly Func<long, string, AtcLogEntry?> parseRootLine;
    private readonly Action<AtcLogEntry> onEntry;
    private readonly List<string> followingLines = [];
    private AtcLogEntry? pendingEntry;
    private bool acceptsFollowingLines;

    public LogEntryAssembler(
        Func<long, string, AtcLogEntry?> parseRootLine,
        Action<AtcLogEntry> onEntry)
    {
        this.parseRootLine = parseRootLine;
        this.onEntry = onEntry;
    }

    public void AddLine(
        long lineNumber,
        string line)
    {
        var startsWithDigit = line.Length > 0 && char.IsDigit(line[0]);
        var rootEntry = startsWithDigit
            ? parseRootLine(lineNumber, line)
            : null;

        if (rootEntry is not null)
        {
            Flush();
            pendingEntry = rootEntry;
            acceptsFollowingLines = true;
            return;
        }

        if (pendingEntry is null)
        {
            return;
        }

        if (startsWithDigit)
        {
            acceptsFollowingLines = false;
            return;
        }

        if (acceptsFollowingLines &&
            followingLines.Count < MaxFollowingLines)
        {
            followingLines.Add(line);
        }
    }

    public void Flush()
    {
        if (pendingEntry is null)
        {
            return;
        }

        var entry = BuildEntry(pendingEntry);
        pendingEntry = null;
        followingLines.Clear();

        onEntry(entry);
    }

    private AtcLogEntry BuildEntry(AtcLogEntry rootEntry)
    {
        var entry = rootEntry;

        var lineCount = followingLines.Count;
        while (lineCount > 0 &&
               string.IsNullOrWhiteSpace(followingLines[lineCount - 1]))
        {
            lineCount--;
        }

        if (lineCount > 0)
        {
            var sb = new StringBuilder(entry.MessageFull);
            for (var i = 0; i < lineCount; i++)
            {
                sb.Append(Environment.NewLine);
                sb.Append(followingLines[i]);
            }

            entry = entry with { MessageFull = sb.ToString() };

            if (entry.MessageFull.IsFormatJson())
            {
                entry = entry with { MessageShort = "[JSON]" };
            }
        }

        if (entry.MessageShort.Length > MessageShortMaxLength)
        {
            entry = entry with
            {
                MessageShort = string.Concat(entry.MessageShort.AsSpan(0, MessageShortMaxLength), "..."),
            };
        }

        return entry;
    }
}