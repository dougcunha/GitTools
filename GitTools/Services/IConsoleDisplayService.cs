using GitTools.Models;

namespace GitTools.Services;

/// <summary>
/// Provides console display operations for git repository operations.
/// </summary>
public interface IConsoleDisplayService
{
    /// <summary>
    /// Gets the hierarchical name of a repository relative to a base folder.
    /// </summary>
    /// <param name="repositoryPath">The full path to the repository.</param>
    /// <param name="baseFolder">The base folder to calculate relative path from.</param>
    /// <returns>The hierarchical name of the repository.</returns>
    string GetHierarchicalName(string repositoryPath, string baseFolder);

    /// <summary>
    /// Shows scan errors if any occurred during repository scanning.
    /// </summary>
    /// <param name="scanErrors">Dictionary of scan errors keyed by repository path.</param>
    /// <param name="baseFolder">The base folder for calculating hierarchical names.</param>
    void ShowScanErrors(Dictionary<string, Exception> scanErrors, string baseFolder);

    /// <summary>
    /// Shows initial information about the operation.
    /// </summary>
    /// <param name="baseFolder">The base folder being scanned.</param>
    /// <param name="tags">The tags being searched for.</param>
    void ShowInitialInfo(string baseFolder, string[] tags);

    /// <summary>
    /// Displays a table of repositories statuses.
    /// </summary>
    /// <remarks>The table includes columns for the repository name, remote URL, and the number of commits the
    /// local branches are ahead or behind the remote branches.</remarks>
    /// <param name="reposStatus">A list of <see cref="GitRepositoryStatus"/> objects representing the status of each repository.</param>
    /// <param name="baseFolder">
    /// The base folder used to calculate the hierarchical names of the repositories.
    /// </param>
    void DisplayRepositoriesStatus(List<GitRepositoryStatus> reposStatus, string baseFolder);
}
