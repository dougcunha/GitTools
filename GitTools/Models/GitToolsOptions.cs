using System.Diagnostics.CodeAnalysis;

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
    public bool LogAllGitCommands { get; set; }

    /// <summary>
    /// Gets or sets the path to the log file where console output will be replicated.
    /// </summary>
    public string? LogFilePath { get; set; }
}
