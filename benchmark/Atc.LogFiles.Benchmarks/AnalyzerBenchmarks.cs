namespace Atc.LogFiles.Benchmarks;

/// <summary>
/// Analyzer hot paths with a large number of entries already loaded.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Disposed in IterationCleanup.")]
public class AnalyzerBenchmarks
{
    private const int LiveEntryCount = 1_000;

    private FakeFileCollector collector = null!;
    private Analyzer.LogAnalyzer analyzer = null!;
    private AtcLogEntry[] liveEntries = null!;

    [Params(200_000)]
    public int LoadedEntryCount { get; set; }

    [IterationSetup]
    public void IterationSetup()
    {
        collector = new FakeFileCollector();
        analyzer = new Analyzer.LogAnalyzer(collector, collector, collector);
        foreach (var entry in SampleLogFactory.CreateEntries(LoadedEntryCount))
        {
            collector.Raise(entry);
        }

        collector.RaiseFilesDone();
        liveEntries = SampleLogFactory.CreateEntries(LiveEntryCount);
    }

    [IterationCleanup]
    public void IterationCleanup()
        => analyzer.Dispose();

    /// <summary>
    /// What the app does for each tailed line: add the entry, then refresh the statistics and source systems.
    /// </summary>
    /// <returns>A checksum, so the work is not optimized away.</returns>
    [Benchmark]
    public long LiveEntries_WithStatisticsAndSourceSystemsPerEntry()
    {
        long total = 0;
        foreach (var entry in liveEntries)
        {
            collector.Raise(entry);
            total += analyzer.GetLogStatistics().Count;
            total += analyzer.GetSourceSystems().Count;
        }

        return total;
    }

    [Benchmark]
    public int GetFilteredLogEntries_WithText()
    {
        analyzer.SetFilter(
            new LogFilter(
                LogLevelTrace: true,
                LogLevelDebug: true,
                LogLevelInfo: true,
                LogLevelWarning: true,
                LogLevelError: true,
                LogLevelCritical: true,
                IncludeText: "order 19",
                DateTimeFrom: null,
                DateTimeTo: null,
                SourceSystem: string.Empty));

        return analyzer.GetFilteredLogEntries().Length;
    }
}