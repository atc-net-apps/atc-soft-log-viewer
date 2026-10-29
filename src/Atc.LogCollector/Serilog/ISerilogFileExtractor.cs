namespace Atc.LogCollector.Serilog;

public interface ISerilogFileExtractor
{
    AtcLogEntry? ParseRootLine(
        string sourceIdentifier,
        string sourceSystem,
        long lineNumber,
        string line);
}