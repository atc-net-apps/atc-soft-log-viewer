namespace Atc.LogFiles.Analyzer;

/// <summary>
/// What the analyzer could not read: lines in another format, and files in another format.
/// </summary>
/// <param name="Lines">Per file, the lines that were not read.</param>
/// <param name="Files">The files that were not read.</param>
public sealed record NotReadSummary(
    IReadOnlyList<LinesNotRead> Lines,
    IReadOnlyList<FileNotRead> Files)
{
    private const int MaxExampleLineLength = 120;

    public bool IsEmpty
        => Lines.Count == 0 && Files.Count == 0;

    /// <summary>
    /// Gets the number of lines that were not read, over all files.
    /// </summary>
    public long LineCount
        => Lines.Sum(x => x.Count);

    /// <summary>
    /// Gets a short text for a status bar, for example "Not read: 5,810 lines, 1 file".
    /// </summary>
    /// <returns>The text; empty when everything was read.</returns>
    public string ToShortText()
    {
        var parts = new List<string>();
        if (Lines.Count > 0)
        {
            parts.Add(Count(LineCount, "line"));
        }

        if (Files.Count > 0)
        {
            parts.Add(Count(Files.Count, "file"));
        }

        return parts.Count == 0
            ? string.Empty
            : "Not read: " + string.Join(", ", parts);
    }

    /// <summary>
    /// Gets the details for a tooltip: the files, an example line per file, and what to do.
    /// </summary>
    /// <param name="maxItems">How many files to name per list.</param>
    /// <returns>The text; empty when everything was read.</returns>
    public string ToDetails(int maxItems = 10)
    {
        if (IsEmpty)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        if (Lines.Count > 0)
        {
            sb.AppendLine("These lines are in another format:");
            foreach (var lines in Lines.Take(maxItems))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"• {Path.GetFileName(lines.FilePath)}: {Count(lines.Count, "line")} ({lines.Format}), the first is line {lines.FirstLineNumber.ToString("N0", CultureInfo.InvariantCulture)}:");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    {Shorten(lines.FirstLine)}");
            }

            AppendMore(sb, Lines.Count, maxItems);
            sb.AppendLine();
        }

        if (Files.Count > 0)
        {
            sb.AppendLine("These files are in another format:");
            foreach (var file in Files.Take(maxItems))
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"• {Path.GetFileName(file.FilePath)} ({file.Format})");
            }

            AppendMore(sb, Files.Count, maxItems);
            sb.AppendLine();
        }

        sb.Append("The viewer reads one format per folder. Open a folder with files in one format.");
        return sb.ToString();
    }

    private static string Count(
        long count,
        string noun)
        => string.Create(CultureInfo.InvariantCulture, $"{count:N0} {noun}{(count == 1 ? string.Empty : "s")}");

    private static string Shorten(string line)
        => line.Length <= MaxExampleLineLength
            ? line
            : string.Concat(line.AsSpan(0, MaxExampleLineLength), "…");

    private static void AppendMore(
        StringBuilder sb,
        int count,
        int maxItems)
    {
        if (count > maxItems)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"… and {count - maxItems} more");
        }
    }
}