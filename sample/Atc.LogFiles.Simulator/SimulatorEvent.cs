namespace Atc.LogFiles.Simulator;

public sealed record SimulatorEvent(
    DateTimeOffset Time,
    string Text,
    bool IsError);