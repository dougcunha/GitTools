using GitTools.Extensions;

namespace GitTools.Services;

/// <summary>
/// Provides tag validation and parsing operations.
/// </summary>
public sealed class TagValidationService : ITagValidationService
{
    /// <inheritdoc />
    public string[] ParseAndValidateTags(string tagsInput)
    {
        if (!IsValidTagsInput(tagsInput))
            return [];

        return [.. tagsInput
            .SplitAndTrim(',')
            .Where(tag => !string.IsNullOrWhiteSpace(tag))];
    }

    /// <inheritdoc />
    public bool IsValidTagsInput(string tagsInput)
        => !string.IsNullOrWhiteSpace(tagsInput);
}
