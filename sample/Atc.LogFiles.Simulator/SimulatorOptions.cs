namespace Atc.LogFiles.Simulator;

/// <summary>
/// Where the simulator writes. Set with <c>Simulator__Folder</c>; the AppHost sets it to <c>sample/output</c>.
/// </summary>
public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public string Folder { get; set; } = Path.Combine(Path.GetTempPath(), "atc-log-simulator");
}