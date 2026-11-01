namespace Atc.LogFiles.SampleGenerator;

/// <summary>
/// What was generated.
/// </summary>
/// <param name="LineCount">The number of lines written.</param>
/// <param name="EntryCount">The number of log entries; an entry can span several lines.</param>
/// <param name="ByteCount">The file size.</param>
/// <param name="Elapsed">How long it took.</param>
public sealed record GeneratorResult(
    long LineCount,
    long EntryCount,
    long ByteCount,
    TimeSpan Elapsed);