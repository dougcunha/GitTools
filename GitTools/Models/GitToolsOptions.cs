using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GitTools.Models;

/// <summary>
/// Represents global application options.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class GitToolsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether all git commands should be logged to the console.
    /// </summary>
    [JsonPropertyName("logAllGitCommands")]
    public bool LogAllGitCommands { get; set; }

    /// <summary>
    /// Gets or sets the path to the log file where console output will be replicated.
    /// </summary>
    [JsonPropertyName("logFilePath")]
    public string? LogFilePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether submodules should be included when scanning for repositories.
    /// </summary>
    [JsonPropertyName("includeSubmodules")]
    public bool IncludeSubmodules { get; set; } = true;

    /// <summary>
    /// Gets or sets the repository filtering patterns using wildcard syntax (* and ?).
    /// When specified, only repositories matching at least one pattern will be processed.
    /// </summary>
    [JsonPropertyName("repositoryFilters")]
    public string[] RepositoryFilters { get; set; } = [];

    /// <summary>
    /// Gets a value indicating whether repository filtering is enabled.
    /// </summary>
    [JsonIgnore]
    public bool HasRepositoryFilters => RepositoryFilters.Length > 0;
}
