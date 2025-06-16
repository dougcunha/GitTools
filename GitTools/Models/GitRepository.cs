namespace GitTools.Models;

/// <summary>
///  Represents a Git repository with its metadata.
///  This class encapsulates the repository's name, path, remote URL, and validity status.
/// </summary>
public sealed class GitRepository
{
    /// <summary>
    ///  Gets or sets the name of the repository.
    ///  This is typically the folder name where the repository is located.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///  Gets or sets the path to the repository.
    ///  This is the full file system path to the repository directory.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    ///  Gets or sets the remote URL of the repository.
    ///  This is the URL of the remote repository, typically where the code is hosted (e.g., GitHub, GitLab).
    /// </summary>
    public string? RemoteUrl { get; init; }

    /// <summary>
    ///  Gets the parent directory of the repository path.
    ///  This is the directory that contains the repository folder.
    /// </summary>
    public string ParentDir
        => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the repository is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Get or sets a value indicating whether the repository has errors while executing git commands.
    /// </summary>
    public bool HasErrors { get; init; }
}