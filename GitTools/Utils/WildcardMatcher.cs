using System.Text.RegularExpressions;

namespace GitTools.Utils;

/// <summary>
/// Utility class for matching strings against wildcard patterns.
/// </summary>
public static class WildcardMatcher
{
    /// <summary>
    /// Determines if a pattern contains wildcard characters.
    /// </summary>
    /// <param name="pattern">The pattern to check.</param>
    /// <returns>True if the pattern contains wildcards; otherwise, false.</returns>
    public static bool IsWildcardPattern(string pattern)
        => pattern.Contains('*') || pattern.Contains('?');

    /// <summary>
    /// Converts a wildcard pattern to a regular expression.
    /// </summary>
    /// <param name="pattern">The wildcard pattern.</param>
    /// <returns>A compiled regular expression.</returns>
    public static Regex ConvertToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern);

        var regexPattern = escaped
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".");

        return new Regex($"^{regexPattern}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Matches a collection of strings against a wildcard pattern.
    /// </summary>
    /// <param name="items">The items to match.</param>
    /// <param name="pattern">The wildcard pattern.</param>
    /// <returns>A list of items that match the pattern.</returns>
    public static List<string> MatchItems(IEnumerable<string> items, string pattern)
    {
        if (!IsWildcardPattern(pattern))
            return items.Contains(pattern) ? [pattern] : [];

        var regex = ConvertToRegex(pattern);

        return [.. items.Where(item => regex.IsMatch(item))];
    }
}
