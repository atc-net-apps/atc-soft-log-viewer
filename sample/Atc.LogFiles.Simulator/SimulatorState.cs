namespace Atc.LogFiles.Simulator;

public sealed record SimulatorState(
    bool IsRunning,
    string Folder,
    SimulatorSettings Settings,
    long EntriesWritten,
    IReadOnlyList<SimulatorFile> Files,
    IReadOnlyList<SimulatorEvent> Events);