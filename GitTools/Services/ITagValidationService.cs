namespace GitTools.Services;

/// <summary>
/// Provides tag validation and parsing operations.
/// </summary>
public interface ITagValidationService
{
    /// <summary>
    /// Validates and parses a comma-separated string of tags.
    /// </summary>
    /// <param name="tagsInput">The comma-separated tags input.</param>
    /// <returns>An array of parsed and trimmed tags, or empty array if input is invalid.</returns>
    string[] ParseAndValidateTags(string tagsInput);

    /// <summary>
    /// Validates if the tags input is not empty or whitespace only.
    /// </summary>
    /// <param name="tagsInput">The tags input to validate.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    bool IsValidTagsInput(string tagsInput);
}
