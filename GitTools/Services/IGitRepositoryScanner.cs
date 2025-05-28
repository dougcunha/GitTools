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
}
