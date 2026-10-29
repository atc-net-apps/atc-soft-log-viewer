namespace Atc.LogCollector.Log4Net;

public class Log4NetFileCollector : LogFileCollectorBase, ILog4NetFileCollector
{
    private static readonly string[] LogExtensions = ["log"];

    private readonly ILog4NetFileExtractor fileExtractor;

    public Log4NetFileCollector(ILog4NetFileExtractor log4NetFileExtractor)
    {
        ArgumentNullException.ThrowIfNull(log4NetFileExtractor);

        fileExtractor = log4NetFileExtractor;
    }

    protected override IReadOnlyCollection<string> DefaultLogExtensions
        => LogExtensions;

    protected override AtcLogEntry? ParseRootLine(
        string sourceIdentifier,
        string sourceSystem,
        long lineNumber,
        string line)
        => fileExtractor.ParseRootLine(
            sourceIdentifier,
            sourceSystem,
            lineNumber,
            line);
}