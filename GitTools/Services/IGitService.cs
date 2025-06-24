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
    /// Retrieves the name of the repository from the specified file path.
    /// </summary>
    /// <param name="path">The file path from which to extract the repository name. This path should be a valid file system path.</param>
    /// <returns>The name of the repository extracted from the given path.</returns>
    static string GetRepositoryName(string path)
       => Path.GetFileName(path.Replace('\\', Path.DirectorySeparatorChar));

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
    /// <param name="branch">
    /// The branch to synchronize.
    /// </param>
    /// <param name="pushNewBranches">
    /// true to push new branches to the remote; otherwise, false.
    /// </param>
    /// <returns>
    /// true if the synchronization was successful; otherwise, false.
    /// </returns>
    Task<bool> SynchronizeBranchAsync(BranchStatus branch, bool pushNewBranches = false);

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
    /// Pop the stashed change in the specified repository.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository to stash changes in.
    /// </param>
    /// <returns>
    /// true if the pop stash was successful; otherwise, false.
    /// </returns>
    Task<bool> PopAsync(string repositoryPath);

    /// <summary>
    /// Gets the status of the specified repository, including local branches and their remote tracking status.
    /// </summary>
    /// <param name="repositoryPath">
    ///     The path to the git repository to get the status of.
    /// </param>
    /// <param name="rootDir"></param>
    /// <param name="fetch">true to fetch updates before gathering status; otherwise, false.</param>
    /// <returns>
    /// A instance of <see cref="GitRepositoryStatus"/> containing the repository's status information.
    /// </returns>
    Task<GitRepositoryStatus> GetRepositoryStatusAsync(string repositoryPath, string rootDir, bool fetch = true);

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
    /// The name of the upstream branch that the specified branch is tracking, if any
    /// </returns>
    Task<(bool isTracked, string? upstream)> IsBranchTrackedAsync(string repositoryPath, string branch);

    /// <summary>
    /// Pushes changes from the specified repository to a remote server.
    /// </summary>
    /// <param name="repositoryPath">The file system path to the local repository to push from. Cannot be null or empty.</param>
    /// <param name="branchName">The name of the branch to push. If null, the current branch is used.</param>
    /// <param name="force">If <see langword="true"/>, forces the push operation, potentially overwriting remote changes. Defaults to <see
    /// langword="false"/>.</param>
    /// <param name="tags">If <see langword="true"/>, includes tags in the push operation. Defaults to <see langword="true"/>.</param>
    /// <returns><see langword="true"/> if the push operation succeeds; otherwise, <see langword="false"/>.</returns>
    Task<bool> PushAsync(string repositoryPath, string? branchName = null, bool force = false, bool tags = true);

    /// <summary>
    /// Checks out the specified branch in the given repository asynchronously.
    /// </summary>
    /// <param name="repoRepoPath">The file system path to the repository where the branch will be checked out. Cannot be null or empty.</param>
    /// <param name="repoCurrentBranch">The name of the branch to check out. If null, nothing will be changed.</param>
    /// <returns>A task that represents the asynchronous checkout operation.</returns>
    Task CheckoutAsync(string repoRepoPath, string? repoCurrentBranch);

    /// <summary>
    /// Synchronizes the specified Git repository with its remote counterpart.
    /// </summary>
    /// <remarks>This method performs a two-way synchronization between the local and remote repositories,
    /// optionally including uncommitted changes and new branches.</remarks>
    /// <param name="repo">The repository status object representing the local repository to synchronize.</param>
    /// <param name="progress">An optional callback action to report progress messages during the synchronization process.</param>
    /// <param name="withUncommited">If <see langword="true"/>, includes uncommitted changes in the synchronization; otherwise, only committed
    /// changes are synchronized.</param>
    /// <param name="pushNewBranches">If <see langword="true"/>, pushes any new branches to the remote repository; otherwise, new branches are not
    /// pushed.</param>
    /// <returns><see langword="true"/> if the synchronization is successful; otherwise, <see langword="false"/>.</returns>
    Task<bool> SynchronizeRepositoryAsync
    (
        GitRepositoryStatus repo,
        Action<FormattableString>? progress,
        bool withUncommited = false,
        bool pushNewBranches = false
    );

    /// <summary>
    /// Gets if the specified branch is the current branch in the repository.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository to check.
    /// </param>
    /// <param name="branch">
    /// The name of the branch to check if it is the current branch.
    /// </param>
    /// <returns>
    /// true if the specified branch is the current branch; otherwise, false.
    /// </returns>
    Task<bool> IsCurrentBranchAsync(string repositoryPath, string branch);
}
