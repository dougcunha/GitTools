using System.Text.Json.Serialization;

namespace GitTools.Models;

/// <summary>
/// Represents a repository backup entry with relative path information.
/// </summary>
public sealed class GitRepositoryBackup
{
    /// <summary>
    /// Gets or sets the name of the repository.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets or sets the relative path of the repository.
    /// </summary>
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    /// <summary>
    /// Gets or sets the remote URL of the repository.
    /// </summary>
    [JsonPropertyName("remote_url")]
    public string? RemoteUrl { get; init; }
}
