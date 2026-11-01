namespace Atc.LogFiles.Collector.Tail;

public record TailLine(
    FileInfo File,
    long LineNumber,
    string Line);