namespace Atc.LogFiles.Simulator;

public sealed record SimulatorFile(
    string Name,
    long SizeBytes,
    DateTime LastWriteTime);