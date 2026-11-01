namespace Atc.LogFiles.Analyzer;

/// <summary>
/// Finds the highlight rule that matches a log entry. The first matching rule wins.
/// </summary>
/// <remarks>
/// The matcher returns the index of the rule, so a front end can keep its own styles (colors and so on)
/// in a list in the same order as the rules.
/// </remarks>
public sealed class HighlightMatcher
{
    /// <summary>
    /// Returned by <see cref="FindMatch(string)"/> when no rule matches.
    /// </summary>
    public const int NoMatch = -1;

    private readonly (int Index, string Text, StringComparison Comparison)[] rules;

    public HighlightMatcher(IReadOnlyList<HighlightRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        this.rules = rules
            .Select((rule, index) => (
                Index: index,
                rule.Text,
                Comparison: rule.IgnoreCase
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
            .Where(rule => !string.IsNullOrEmpty(rule.Text))
            .ToArray();
    }

    /// <summary>
    /// Finds the first rule that matches the full message of the entry.
    /// </summary>
    /// <param name="entry">The log entry.</param>
    /// <returns>The index of the rule, or <see cref="NoMatch"/>.</returns>
    public int FindMatch(AtcLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return FindMatch(entry.MessageFull);
    }

    /// <summary>
    /// Finds the first rule that matches the message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The index of the rule, or <see cref="NoMatch"/>.</returns>
    public int FindMatch(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        foreach (var rule in rules)
        {
            if (message.Contains(rule.Text, rule.Comparison))
            {
                return rule.Index;
            }
        }

        return NoMatch;
    }
}