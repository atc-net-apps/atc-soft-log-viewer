namespace Atc.LogViewer.SampleGenerator;

/// <summary>
/// What to generate.
/// </summary>
/// <param name="Format">The log file format.</param>
/// <param name="LineCount">The exact number of lines to write.</param>
/// <param name="OutputPath">The file to write; it is overwritten when it exists.</param>
/// <param name="Seed">The random seed; the same seed gives the same content.</param>
public sealed record GeneratorOptions(
    LogFormat Format,
    long LineCount,
    string OutputPath,
    int Seed = 42);