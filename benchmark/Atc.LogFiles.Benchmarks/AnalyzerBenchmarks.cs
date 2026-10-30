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

    [IterationSetup(Target = nameof(ApplyFilter_AfterAParallelLoad))]
    public void ParallelLoadSetup()
    {
        collector = new FakeFileCollector();
        analyzer = new Analyzer.LogAnalyzer(collector, collector, collector);

        // Four files read at the same time: each file's entries in order, the files in turns of 1,000
        var entries = SampleLogFactory.CreateEntries(LoadedEntryCount);
        foreach (var chunk in entries
                     .Select((entry, index) => (entry, index))
                     .GroupBy(x => x.index % 4)
                     .SelectMany(file => file.Chunk(1_000).Select((part, turn) => (turn, part)))
                     .OrderBy(x => x.turn))
        {
            foreach (var (entry, _) in chunk.part)
            {
                collector.Raise(entry);
            }
        }

        collector.RaiseFilesDone();
        analyzer.ApplyFilter(AllLevels(string.Empty));
    }

    private static LogFilter AllLevels(string includeText)
        => new(
            LogLevelTrace: true,
            LogLevelDebug: true,
            LogLevelInfo: true,
            LogLevelWarning: true,
            LogLevelError: true,
            LogLevelCritical: true,
            IncludeText: includeText,
            DateTimeFrom: null,
            DateTimeTo: null,
            SourceSystem: string.Empty);

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

    /// <summary>
    /// A filter change after the folder is loaded: the files were read in parallel, so their entries
    /// arrived interleaved, and the first filter after the load has already run.
    /// </summary>
    /// <returns>The number of matching entries.</returns>
    [Benchmark]
    public int ApplyFilter_AfterAParallelLoad()
        => analyzer.ApplyFilter(AllLevels("order")).Entries.Count;

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