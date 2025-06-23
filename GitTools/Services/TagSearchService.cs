using GitTools.Utils;

namespace GitTools.Services;

/// <summary>
/// Provides tag search operations across git repositories.
/// </summary>
public sealed class TagSearchService(IGitRepositoryScanner gitScanner, IGitService gitService) : ITagSearchService
{
    private static string GetRepositoryName(string path)
        => Path.GetFileName(path.Replace('\\', Path.DirectorySeparatorChar));
    /// <inheritdoc />
    public async Task<TagSearchResult> SearchRepositoriesWithTagsAsync
    (
        string baseFolder,
        string[] tagsToSearch,
        Action<string>? progressCallback = null
    )
    {
        var allGitFolders = gitScanner.Scan(baseFolder);
        var repositoriesWithTags = new List<string>();
        var repositoryTagsMap = new Dictionary<string, List<string>>();
        var scanErrors = new Dictionary<string, Exception>();

        foreach (var repo in allGitFolders)
        {
            progressCallback?.Invoke(GetRepositoryName(repo));

            try
            {
                var foundTags = await SearchTagsInRepositoryAsync(repo, tagsToSearch).ConfigureAwait(false);

                if (foundTags.Count <= 0)
                    continue;

                repositoriesWithTags.Add(repo);
                repositoryTagsMap[repo] = foundTags;
            }
            catch (Exception ex)
            {
                scanErrors[repo] = ex;
            }
        }

        return new TagSearchResult(repositoriesWithTags, repositoryTagsMap, scanErrors);
    }

    /// <inheritdoc />
    public async Task<List<string>> SearchTagsInRepositoryAsync(string repositoryPath, string[] tagsToSearch)
    {
        var allRepoTags = await gitService.GetAllTagsAsync(repositoryPath).ConfigureAwait(false);
        var foundTags = new List<string>();

        foreach (var tagPattern in tagsToSearch)
        {
            var matchingTags = WildcardMatcher.MatchItems(allRepoTags, tagPattern);
            foundTags.AddRange(matchingTags);
        }

        return [.. foundTags.Distinct()];
    }
}
