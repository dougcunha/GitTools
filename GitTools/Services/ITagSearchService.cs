namespace GitTools.Services;

/// <summary>
/// Represents the result of a tag search operation.
/// </summary>
/// <param name="RepositoriesWithTags">Repositories that contain the searched tags.</param>
/// <param name="RepositoryTagsMap">Mapping of repository paths to found tags.</param>
/// <param name="ScanErrors">Errors that occurred during scanning.</param>
public sealed record TagSearchResult
(
    List<string> RepositoriesWithTags,
    Dictionary<string, List<string>> RepositoryTagsMap,
    Dictionary<string, Exception> ScanErrors
);

/// <summary>
/// Provides tag search operations across git repositories.
/// </summary>
public interface ITagSearchService
{
    /// <summary>
    /// Searches for repositories containing specified tags.
    /// </summary>
    /// <param name="baseFolder">The base folder to scan for repositories.</param>
    /// <param name="tagsToSearch">The tags to search for (supports wildcards).</param>
    /// <param name="progressCallback">Optional callback for progress reporting.</param>
    /// <returns>The search result containing found repositories and any errors.</returns>
    Task<TagSearchResult> SearchRepositoriesWithTagsAsync
    (
        string baseFolder,
        string[] tagsToSearch,
        Action<string>? progressCallback = null
    );

    /// <summary>
    /// Searches for tags in a specific repository.
    /// </summary>
    /// <param name="repositoryPath">The path to the repository.</param>
    /// <param name="tagsToSearch">The tags to search for (supports wildcards).</param>
    /// <returns>List of found tags in the repository.</returns>
    Task<List<string>> SearchTagsInRepositoryAsync(string repositoryPath, string[] tagsToSearch);
}
