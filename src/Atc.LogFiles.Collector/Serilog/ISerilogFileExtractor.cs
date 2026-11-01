namespace Atc.LogFiles.Collector.Serilog;

public interface ISerilogFileExtractor
{
    AtcLogEntry? ParseRootLine(
        string sourceIdentifier,
        string sourceSystem,
        long lineNumber,
        string line);
}