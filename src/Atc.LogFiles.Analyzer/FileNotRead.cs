namespace Atc.LogFiles.Analyzer;

/// <summary>
/// A log file in the watched folder that is in another format than the one being read, so it is not read.
/// </summary>
/// <param name="FilePath">The file.</param>
/// <param name="Format">The format the file is in.</param>
public sealed record FileNotRead(
    string FilePath,
    LogFileCollectorType Format);