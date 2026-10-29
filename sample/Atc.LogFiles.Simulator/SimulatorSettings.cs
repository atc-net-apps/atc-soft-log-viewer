namespace Atc.LogFiles.Simulator;

/// <summary>
/// What the simulator writes.
/// </summary>
/// <param name="Format">The log file layout.</param>
/// <param name="LinesPerSecond">Entries per second, over all files.</param>
/// <param name="FileCount">The number of services, each with its own file (1-8).</param>
/// <param name="ErrorPercent">The share of entries that are errors.</param>
/// <param name="StackTracePercent">The share of errors that have a stack trace on the following lines.</param>
/// <param name="RollMode">How files are rolled.</param>
/// <param name="RollSizeKb">Roll a file when it reaches this size; 0 rolls only on request.</param>
public sealed record SimulatorSettings(
    LogFormat Format = LogFormat.Serilog,
    int LinesPerSecond = 5,
    int FileCount = 2,
    int ErrorPercent = 5,
    int StackTracePercent = 50,
    RollMode RollMode = RollMode.Rename,
    int RollSizeKb = 0)
{
    public SimulatorSettings Normalize()
        => this with
        {
            LinesPerSecond = Math.Clamp(LinesPerSecond, 0, 10_000),
            FileCount = Math.Clamp(FileCount, 1, 8),
            ErrorPercent = Math.Clamp(ErrorPercent, 0, 100),
            StackTracePercent = Math.Clamp(StackTracePercent, 0, 100),
            RollSizeKb = Math.Max(RollSizeKb, 0),
        };
}