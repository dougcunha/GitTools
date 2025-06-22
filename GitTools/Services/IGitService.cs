using GitTools.Models;

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

    /// <summary>
    /// Gets the git repository information for the specified repository name.
    /// </summary>
    /// <param name="repositoryPath">The path of the repository to retrieve.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains the GitRepository object.
    /// </returns>
    Task<GitRepository> GetGitRepositoryAsync(string repositoryPath);

    /// <summary>
    /// Deletes a local git repository at the specified path.
    /// This method will remove the git repository directory and all its contents.
    /// </summary>
    /// <param name="repositoryPath">
    ///  The path to the local git repository to delete.
    /// </param>
    /// <returns>
    /// true if the repository was successfully deleted; otherwise, false.
    /// If the repository does not exist or cannot be deleted, it returns false.
    /// </returns>
    Task<bool> DeleteLocalGitRepositoryAsync(string? repositoryPath);

    /// <summary>
    /// Checks if the repository has uncommitted changes.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository to check for uncommitted changes.
    /// </param>
    /// <returns>
    /// true if there are uncommitted changes; otherwise, false.
    /// </returns>
    Task<bool> HasUncommittedChangesAsync(string repositoryPath);

    /// <summary>
    /// Gets the number of commits the remote repository is ahead/behind of the local repository.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository to check.
    /// </param>
    /// <param name="branchName">
    /// The name of the branch to check for ahead/behind status.
    /// </param>
    /// <param name="fetch">
    /// true to fetch updates from the remote repository before checking; otherwise, false.
    /// </param>
    /// <returns>
    /// The number of commits the remote repository is ahead/behind of the local repository.
    /// </returns>
    Task<(int ahead, int behind)> GetRemoteAheadBehindCountAsync(string repositoryPath, string branchName, bool fetch = true);

    /// <summary>
    /// Fetches updates from the remote repository.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository to fetch updates from.
    /// </param>
    /// <param name="prune">
    /// true to prune deleted branches; otherwise, false.
    /// </param>
    Task<bool> FetchAsync(string repositoryPath, bool prune = false);

    /// <summary>
    /// Gets a list of all local branches in the specified repository.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository to list branches from.
    /// </param>
    /// <returns>
    /// A list of local branch names in the repository.
    /// </returns>
    Task<List<string>> GetLocalBranchesAsync(string repositoryPath);

    /// <summary>
    /// Synchronizes the specified branch in the repository with the remote.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository to synchronize.
    /// </param>
    /// <param name="branchName">
    /// The name of the branch to synchronize.
    /// </param>
    /// <returns>
    /// true if the synchronization was successful; otherwise, false.
    /// </returns>
    Task<bool> SynchronizeBranchAsync(string repositoryPath, string branchName);

    /// <summary>
    /// Stashes uncommitted changes in the specified repository.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository to stash changes in.
    /// </param>
    /// <param name="includeUntracked">
    /// true to include untracked files in the stash; otherwise, false.
    /// </param>
    /// <returns>
    /// true if the stash was successful; otherwise, false.
    /// </returns>
    Task<bool> StashAsync(string repositoryPath, bool includeUntracked = false);

    /// <summary>
    /// Gets the status of the specified repository, including local branches and their remote tracking status.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository to get the status of.
    /// </param>
    /// <returns>
    /// A instance of <see cref="GitRepositoryStatus"/> containing the repository's status information.
    /// </returns>
    Task<GitRepositoryStatus> GetRepositoryStatusAsync(string repositoryPath);

    /// <summary>
    /// Checks if a specific branch is tracked by the remote repository.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository to check.
    /// </param>
    /// <param name="branch">
    /// The name of the branch to check for tracking status.
    /// </param>
    /// <returns>
    /// true if the branch is tracked by a remote branch; otherwise, false.
    /// </returns>
    Task<bool> IsBranchTrackedAsync(string repositoryPath, string branch);
}
