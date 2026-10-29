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
    public bool IsEmpty
        => Lines.Count == 0 && Files.Count == 0;
}