namespace Atc.LogFiles.Analyzer;

/// <summary>
/// Lines in a file that start an entry in another format than the one being read.
/// </summary>
/// <param name="FilePath">The file.</param>
/// <param name="Count">How many lines were not read.</param>
/// <param name="FirstLineNumber">The 1-based number of the first such line.</param>
/// <param name="FirstLine">The first such line, as an example.</param>
/// <param name="Format">The format the first such line is in.</param>
public sealed record LinesNotRead(
    string FilePath,
    long Count,
    long FirstLineNumber,
    string FirstLine,
    LogFileCollectorType Format);