namespace Atc.LogFiles.Analyzer;

/// <summary>
/// Lines in a file that look like the start of an entry but are not in the format being read.
/// </summary>
/// <param name="FilePath">The file.</param>
/// <param name="Count">How many lines were not read.</param>
/// <param name="FirstLineNumber">The 1-based number of the first such line.</param>
/// <param name="FirstLine">The first such line, as an example.</param>
public sealed record LinesNotRead(
    string FilePath,
    long Count,
    long FirstLineNumber,
    string FirstLine);