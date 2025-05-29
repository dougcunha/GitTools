namespace GitTools.Services;

/// <summary>
/// Provides tag-related operations for git repositories.
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Removes the given tag from the remote repository.
    /// This method will delete the tag from the remote repository specified by the repoPath.
    /// </summary>
    /// <param name="repoPath">
    /// The path to the git repository.
    /// </param>
    /// <param name="tag">
    /// The name of the tag to delete.
    /// </param>
    Task DeleteRemoteTagAsync(string repoPath, string tag);

    /// <summary>
    /// Removes the given tag from the repository locally.
    /// This method will delete the tag from the local repository specified by the repoPath.
    /// </summary>
    /// <param name="repoPath">
    /// The path to the git repository.
    /// </param>
    /// <param name="tag">
    /// The name of the tag to delete.
    /// </param>
    Task DeleteTagAsync(string repoPath, string tag);

    /// <summary>
    /// Checks if the repository has the given tag.
    /// This method will check if the specified tag exists in the local repository.
    /// </summary>
    /// <param name="repoPath">
    /// The path to the git repository.
    /// </param>
    /// <param name="tag">
    /// The name of the tag to check.
    /// </param>
    /// <returns>
    /// True if the tag exists; otherwise, false.
    /// </returns>
    Task<bool> HasTagAsync(string repoPath, string tag);

    /// <summary>
    /// Lists all tags in the repository.
    /// </summary>
    /// <param name="repoPath">The path to the git repository.</param>
    /// <returns>A list of all tag names in the repository.</returns>
    Task<List<string>> GetAllTagsAsync(string repoPath);

    /// <summary>
    /// Lists all tags in the repository that match the specified pattern.
    /// </summary>
    /// <param name="repoPath">The path to the git repository.</param>
    /// <param name="pattern">The wildcard pattern to match tags against (e.g., "v1.*", "release-*").</param>
    /// <returns>A list of tag names that match the pattern.</returns>
    Task<List<string>> GetTagsMatchingPatternAsync(string repoPath, string pattern);

    /// <summary>
    /// Runs a git command in the correct repository directory.
    /// This method executes a git command with the specified arguments in the given working directory.
    /// </summary>
    /// <param name="workingDirectory">
    /// The working directory to run the git command in.
    /// </param>
    /// <param name="arguments">
    /// The arguments to pass to the git command.
    /// </param>
    /// <returns>
    /// The output of the git command.
    /// </returns>
    Task<string> RunGitCommandAsync(string workingDirectory, string arguments);
}
