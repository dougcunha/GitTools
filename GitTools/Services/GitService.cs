using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;
using System.Text.RegularExpressions;

namespace GitTools.Services;

/// <summary>
/// Provides tag-related operations for git repositories.
/// </summary>
public sealed class GitService(IFileSystem fileSystem, IProcessRunner processRunner) : IGitService
{
    /// <inheritdoc/>
    public async Task<bool> HasTagAsync(string repoPath, string tag)
    {
        var result = await RunGitCommandAsync(repoPath, $"tag -l {tag}").ConfigureAwait(false);

        return !string.IsNullOrWhiteSpace(result);
    }

    /// <inheritdoc/>
    public Task DeleteTagAsync(string repoPath, string tag)
        => RunGitCommandAsync(repoPath, $"tag -d {tag}");

    /// <inheritdoc/>
    public Task DeleteRemoteTagAsync(string repoPath, string tag)
        => RunGitCommandAsync(repoPath, $"push origin :refs/tags/{tag}");

    /// <inheritdoc/>
    public async Task<List<string>> GetAllTagsAsync(string repoPath)
    {
        var result = await RunGitCommandAsync(repoPath, "tag -l").ConfigureAwait(false);

        return string.IsNullOrWhiteSpace(result)
            ? []
            : [.. result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetTagsMatchingPatternAsync(string repoPath, string pattern)
    {
        var allTags = await GetAllTagsAsync(repoPath).ConfigureAwait(false);
        var regex = ConvertWildcardToRegex(pattern);

        return [.. allTags.Where(tag => regex.IsMatch(tag))];
    }

    /// <inheritdoc/>
    public async Task<string> RunGitCommandAsync(string workingDirectory, string arguments)
    {
        var realWorkingDirectory = GetRealGitDirectory(workingDirectory);

        var output = new StringBuilder();
        var error = new StringBuilder();

        var exitCode = await processRunner.RunAsync
        (
            new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = realWorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            (_, e) => { if (e.Data != null) output.AppendLine(e.Data); },
            (_, e) => { if (e.Data != null) error.AppendLine(e.Data); }
        ).ConfigureAwait(false);

        return exitCode != 0
            ? throw new InvalidOperationException(error.ToString())
            : output.ToString();
    }

    /// <summary>
    /// Gets the real git directory for the specified repository path.
    /// This method checks if the .git directory exists directly in the repository path,
    /// or if it's a git worktree, it will find the main repository directory.
    /// </summary>
    /// <param name="repoPath">
    /// The path to the git repository.
    /// </param>
    /// <returns>
    /// The path to the real git directory.
    /// </returns>
    private string GetRealGitDirectory(string repoPath)
    {
        const string GIT_DIR = ".git";
        var gitPath = Path.Combine(repoPath, GIT_DIR);

        if (fileSystem.Directory.Exists(gitPath) || !fileSystem.File.Exists(gitPath))
            return repoPath;

        var content = fileSystem.File.ReadAllText(gitPath).Trim();

        if (!content.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
            return repoPath;

        var relativePath = content[7..].Trim();

        return Path.GetFullPath(Path.Combine(repoPath, relativePath));
    }

    /// <summary>
    /// Converts a wildcard pattern to a regular expression.
    /// </summary>
    /// <param name="pattern">The wildcard pattern (e.g., "v1.*", "release-*").</param>
    /// <returns>A compiled regular expression.</returns>
    private static Regex ConvertWildcardToRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern);
        
        var regexPattern = escaped
            .Replace(@"\*", ".*")
            .Replace(@"\?", ".");

        return new Regex($"^{regexPattern}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
