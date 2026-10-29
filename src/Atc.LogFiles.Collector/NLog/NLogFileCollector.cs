namespace Atc.LogFiles.Collector.NLog;

public class NLogFileCollector : LogFileCollectorBase, INLogFileCollector
{
    private static readonly string[] LogExtensions = ["log"];

    private readonly INLogFileExtractor fileExtractor;

    public NLogFileCollector(INLogFileExtractor nlogFileExtractor)
    {
        ArgumentNullException.ThrowIfNull(nlogFileExtractor);

        fileExtractor = nlogFileExtractor;
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