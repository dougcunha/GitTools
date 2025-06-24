namespace GitTools.Services;

/// <summary>
/// Interface for scanning directories to find Git repositories and submodules.
/// </summary>
public interface IGitRepositoryScanner
{
    /// <summary>
    /// Finds all Git repositories and submodules from the root folder.
    /// </summary>
    /// <param name="rootFolder">Root directory to scan.</param>
    List<string> Scan(string rootFolder);

    /// <summary>
    /// Finds Git repositories from the root folder with optional submodule inclusion.
    /// </summary>
    /// <param name="rootFolder">Root directory to scan.</param>
    /// <param name="includeSubmodules">Whether to include submodules in the scan.</param>
    List<string> Scan(string rootFolder, bool includeSubmodules);
}
