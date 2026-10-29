namespace Atc.LogCollector.NLog;

public interface INLogFileExtractor
{
    AtcLogEntry? ParseRootLine(
        string sourceIdentifier,
        string sourceSystem,
        long lineNumber,
        string line);
}