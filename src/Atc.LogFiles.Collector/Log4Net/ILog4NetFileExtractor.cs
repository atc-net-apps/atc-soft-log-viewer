namespace Atc.LogFiles.Collector.Log4Net;

public interface ILog4NetFileExtractor
{
    AtcLogEntry? ParseRootLine(
        string sourceIdentifier,
        string sourceSystem,
        long lineNumber,
        string line);
}