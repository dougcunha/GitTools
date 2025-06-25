namespace GitTools.Services;

/// <summary>
/// Interface for scanning directories to find Git repositories and submodules.
/// </summary>
public interface IGitRepositoryScanner
{
    /// <summary>
    /// Finds all Git repositories from the root folder.
    /// Submodule inclusion is controlled by the global option.
    /// </summary>
    /// <param name="rootFolder">Root directory to scan.</param>
    List<string> Scan(string rootFolder);
}
