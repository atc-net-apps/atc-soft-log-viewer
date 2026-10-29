namespace Atc.LogAnalyzer;

/// <summary>
/// Log entries that match the filter with the given <paramref name="FilterVersion"/>.
/// </summary>
/// <param name="Entries">The entries, ordered by time stamp.</param>
/// <param name="FilterVersion">
/// Increases each time the filter is applied. A batch with an older version than the last applied filter
/// only contains entries that are already in that filter's result.
/// </param>
public sealed record LogEntryBatch(
    IReadOnlyList<AtcLogEntry> Entries,
    long FilterVersion);