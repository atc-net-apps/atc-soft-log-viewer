namespace Atc.LogFiles.Benchmarks;

/// <summary>
/// Initial load of a large Serilog file (no monitoring).
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class CollectorBenchmarks
{
    private FileInfo file = null!;
    private SerilogFileCollector collector = null!;
    private int collected;

    [Params(200_000)]
    public int EntryCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        file = SampleLogFactory.CreateSerilogFile(EntryCount);
        collector = new SerilogFileCollector(new SerilogFileExtractor());
        collector.CollectedEntry += (_, _) => collected++;
    }

    [GlobalCleanup]
    public void GlobalCleanup()
        => file.Directory!.Delete(recursive: true);

    [Benchmark]
    public async Task<int> CollectSerilogFile()
    {
        collected = 0;
        await collector.CollectFile(
            file,
            new LogFileCollectorConfiguration(),
            useMonitoring: false,
            CancellationToken.None).ConfigureAwait(false);

        return collected;
    }
}