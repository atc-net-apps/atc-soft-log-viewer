namespace Atc.LogFiles.Collector.Serilog;

public class SerilogFileCollector : LogFileCollectorBase, ISerilogFileCollector
{
    private static readonly string[] LogExtensions = ["log", "txt"];

    private readonly ISerilogFileExtractor fileExtractor;

    public SerilogFileCollector(ISerilogFileExtractor serilogFileExtractor)
    {
        ArgumentNullException.ThrowIfNull(serilogFileExtractor);

        fileExtractor = serilogFileExtractor;
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