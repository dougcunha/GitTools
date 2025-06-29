namespace GitTools.Extensions;

/// <summary>
/// Provides extension methods for string operations commonly used in Git operations.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Splits a string by newlines, removing empty entries and trimming whitespace.
    /// </summary>
    /// <param name="value">The string to split.</param>
    /// <returns>An array of strings split by newlines with empty entries removed and whitespace trimmed.</returns>
    public static string[] SplitLines(this string value)
        => value.SplitAndTrim('\n');

    /// <summary>
    /// Splits a string by the specified separator, removing empty entries and trimming whitespace.
    /// </summary>
    /// <param name="value">The string to split.</param>
    /// <param name="separator">The character to split by.</param>
    /// <returns>An array of strings split by the separator with empty entries removed and whitespace trimmed.</returns>
    public static string[] SplitAndTrim(this string value, char separator)
        => value.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
