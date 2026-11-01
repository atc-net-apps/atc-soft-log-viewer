namespace Atc.LogFiles.Benchmarks;

/// <summary>
/// BenchmarkDotNet entry point. Run with `dotnet run -c Release --project benchmark/Atc.LogFiles.Benchmarks -- --filter '*'`.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
        => BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args);
}