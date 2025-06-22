namespace GitTools.Models;

/// <summary>
/// Represents the status of a Git repository.
/// This record contains information about the repository's name, path, remote URL, error status,
/// </summary>
/// <param name="Name">
/// The name of the repository, typically the folder name.
/// </param>
/// <param name="HierarchicalName">
/// The hierarchical name of the repository, which is a relative path from a base folder.
/// </param>
/// <param name="RepoPath">
/// The file system path to the repository directory.
/// </param>
/// <param name="RemoteUrl">
/// The remote URL of the repository, where the code is hosted (e.g., GitHub, GitLab).
/// If the repository does not have a remote, this can be null.
/// </param>
/// <param name="HasUncommitedChanges">
/// A value indicating whether the repository has uncommitted changes.
/// </param>
/// <param name="LocalBranches">
/// A list of local branches in the repository, each represented by a <see cref="BranchStatus"/>.
/// Each branch status includes the branch name, whether it is valid, and how many commits it is ahead of the remote.
/// </param>
/// <param name="ErrorMessage">
/// An optional error message if there was an issue retrieving the repository status.
/// If there are no errors, this can be null.
/// </param>
public record GitRepositoryStatus
(
    string Name,
    string HierarchicalName,
    string RepoPath,
    string? RemoteUrl,
    bool HasUncommitedChanges,
    List<BranchStatus> LocalBranches,
    string? ErrorMessage = null
)
{
    /// <summary>
    /// Gets the parent directory of the repository path.
    /// This is the directory that contains the repository folder.
    /// </summary>
    public string ParentDir
        => Path.GetDirectoryName(RepoPath) ?? string.Empty;

    /// <summary>
    /// Gets a value indicating whether the repository is valid.
    /// A repository is considered valid if it has no errors and has a remote URL.
    /// </summary>
    public bool HasErros
        => !string.IsNullOrWhiteSpace(ErrorMessage);

    /// <summary>
    /// Getis if all the local branches in the repository are synced with their remote counterparts.
    /// A branch is considered synced if it has no commits ahead or behind the remote branch.
    /// </summary>
    public bool AreBranchesSynced
        => LocalBranches.All(branch => branch.IsSynced);

    /// <summary>
    /// Gets the number of local branches that do not have an upstream branch configured.
    /// </summary>
    public int UntrackedBranchesCount
        => LocalBranches.Count(branch => string.IsNullOrWhiteSpace(branch.Upstream));

    /// <summary>
    /// Gets the number of tracked branches in the repository.
    /// </summary>
    public int TrackedBranchesCount
        => LocalBranches.Count(branch => !string.IsNullOrWhiteSpace(branch.Upstream));

    /// <summary>
    /// Gets the name of the current branch in the repository.
    /// </summary>
    public string? CurrentBranch
        => LocalBranches.FirstOrDefault(branch => branch.IsCurrent)?.Name;
};

/// <summary>
/// Represents the status of a local branch in a Git repository.
/// This record contains the branch name, whether it is valid, and how many commits it is ahead of the remote.
/// </summary>
/// <param name="RepositoryPath">
/// The path to the repository that contains this branch.
/// </param>
/// <param name="Name">
/// The name of the branch.
/// </param>
/// <param name="Upstream">
/// The name of the upstream branch this branch is tracking.
/// </param>
/// <param name="IsCurrent">
/// true if this branch is the currently checked out branch; otherwise, false.
/// </param>
/// <param name="RemoteAheadCount">
/// The number of commits the remote branch is ahead of the local branch.
/// </param>
/// <param name="RemoteBehindCount">
/// The number of commits the local branch is ahead of the remote branch.
/// </param>
public record BranchStatus
(
    string RepositoryPath,
    string Name,
    string? Upstream,
    bool IsCurrent,
    int RemoteAheadCount,
    int RemoteBehindCount
)
{
    /// <summary>
    /// Gets if the branch is in sync with its remote counterpart.
    /// A branch is considered in sync if it has no commits ahead or behind the remote branch.
    /// </summary>
    public bool IsSynced
        => RemoteAheadCount == 0 && RemoteBehindCount == 0;

    /// <summary>
    /// Gets if the branch is tracked by an upstream branch.
    /// </summary>
    public bool IsTracked
        => !string.IsNullOrWhiteSpace(Upstream);
}
