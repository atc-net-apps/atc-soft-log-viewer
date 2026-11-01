namespace Atc.LogFiles.Analyzer;

/// <summary>
/// What the list shows.
/// </summary>
/// <param name="SourceSystems">The source systems to show; empty shows every source system.</param>
public record LogFilter(
    bool LogLevelTrace,
    bool LogLevelDebug,
    bool LogLevelInfo,
    bool LogLevelWarning,
    bool LogLevelError,
    bool LogLevelCritical,
    string IncludeText,
    DateTime? DateTimeFrom,
    DateTime? DateTimeTo,
    IReadOnlySet<string> SourceSystems)
{
    /// <summary>
    /// Gets the source systems to filter on from the ticked ones. With every one ticked, the set is empty,
    /// so a source system read later shows too.
    /// </summary>
    /// <param name="ticked">The ticked source systems.</param>
    /// <param name="available">Every source system read so far.</param>
    /// <returns>The set for <see cref="SourceSystems"/>.</returns>
    public static IReadOnlySet<string> SourceSystemsToShow(
        IEnumerable<string> ticked,
        IEnumerable<string> available)
    {
        ArgumentNullException.ThrowIfNull(ticked);
        ArgumentNullException.ThrowIfNull(available);

        var show = new HashSet<string>(ticked, StringComparer.Ordinal);
        if (available.All(show.Contains))
        {
            show.Clear();
        }

        return show;
    }
}