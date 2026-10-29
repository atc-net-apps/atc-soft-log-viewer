namespace Atc.LogAnalyzer;

/// <summary>
/// A highlight rule: log entries whose full message contains <see cref="Text"/> are highlighted.
/// How a match is shown (colors and so on) is up to the front end.
/// </summary>
/// <param name="Text">The text to look for. A rule without text never matches.</param>
/// <param name="IgnoreCase">Whether to ignore casing when looking for the text.</param>
public sealed record HighlightRule(
    string Text,
    bool IgnoreCase = false);