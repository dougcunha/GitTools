using System.Diagnostics;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using GitTools.Models;
using Spectre.Console;

namespace GitTools.Services;

/// <summary>
/// Provides tag-related operations for git repositories.
/// </summary>
public sealed partial class GitService(IFileSystem fileSystem, IProcessRunner processRunner, IAnsiConsole console) : IGitService
{
    [GeneratedRegex(@"^\s*url\s*=\s*(.+)$")]
    private static partial Regex RegexUrl();

    [GeneratedRegex("""\[(?<section>remote|submodule)\s+"(?<name>[^"]+)"\]""", RegexOptions.Compiled)]
    private static partial Regex RegexConfigSection();

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

        return exitCode != 0 && !string.IsNullOrWhiteSpace(error.ToString())
            ? throw new InvalidOperationException(error.ToString().Trim())
            : output.ToString().Trim();
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

    public async Task<GitRepository> GetGitRepositoryAsync(string repositoryPath)
    {
        var valid = fileSystem.Directory.Exists(repositoryPath) &&
            (fileSystem.Directory.Exists(Path.Combine(repositoryPath, ".git")) || fileSystem.File.Exists(Path.Combine(repositoryPath, ".git")));

        var repoName = fileSystem.Path.GetFileName(repositoryPath);

        if (string.IsNullOrWhiteSpace(repoName))
            repoName = repositoryPath;

        if (!valid)
            return new GitRepository { Name = repoName, Path = repositoryPath, IsValid = false, HasErrors = true };

        string? remoteUrl = null;

        try
        {
            remoteUrl = (await RunGitCommandAsync(repositoryPath, "config --get remote.origin.url").ConfigureAwait(false)).Trim();
        }
        catch
        {
            // Try to get the remote url directly from config
            var configPath = Path.Combine(repositoryPath, ".git", "config");

            if (fileSystem.File.Exists(configPath))
                remoteUrl = await GetUrlFromGitConfigAsync(configPath, "origin").ConfigureAwait(false);

            return new GitRepository
            {
                Name = repoName,
                Path = repositoryPath,
                RemoteUrl = remoteUrl,
                IsValid = !string.IsNullOrWhiteSpace(remoteUrl),
                HasErrors = true
            };
        }

        return new GitRepository
        {
            Name = repoName,
            Path = repositoryPath,
            RemoteUrl = remoteUrl,
            IsValid = valid,
            HasErrors = false
        };
    }

    private async Task<string?> GetUrlFromGitConfigAsync(string configPath, string sectionName)
    {
        var sectionPattern = RegexConfigSection();

        string? currentName = null;

        await foreach (var line in fileSystem.File.ReadLinesAsync(configPath))
        {
            var sectionMatch = sectionPattern.Match(line);

            if (sectionMatch.Success)
            {
                currentName = sectionMatch.Groups["name"].Value;

                continue;
            }

            if (currentName != sectionName)
                continue;

            var urlMatch = RegexUrl().Match(line);

            if (urlMatch.Success)
                return urlMatch.Groups[1].Value.Trim();
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteLocalGitRepositoryAsync(string? repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
            return false;

        try
        {
            var resultCode = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? await processRunner.RunAsync
                (
                    new ProcessStartInfo
                    {
                        FileName = "powershell",
                        Arguments = $"-Command \"Remove-Item -Recurse -Force '{repositoryPath}'\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                ).ConfigureAwait(false)
                : await processRunner.RunAsync
                (
                    new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"rm -rf '{repositoryPath}'\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                ).ConfigureAwait(false);

            if (resultCode != 0)
            {
                console.MarkupInterpolated($"[red]Error deleting repository {repositoryPath}: result code {resultCode}[/]");

                return false;
            }

            return !fileSystem.Directory.Exists(repositoryPath);
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error deleting repository {repositoryPath}: {ex.Message}[/]");

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> HasUncommittedChangesAsync(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return false;

        try
        {
            var status = await RunGitCommandAsync(repositoryPath, "status --porcelain").ConfigureAwait(false);

            return !string.IsNullOrWhiteSpace(status);
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error checking uncommitted changes in {repositoryPath}: {ex.Message}[/]");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<(int ahead, int behind)> GetRemoteAheadBehindCountAsync(string repositoryPath, string branchName, bool fetch = true)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return (0, 0);

        try
        {
            if (fetch)
                await RunGitCommandAsync(repositoryPath, "fetch").ConfigureAwait(false);

            var countStr = await RunGitCommandAsync
            (
                repositoryPath,
                $"rev-list --left-right --count {branchName}...origin/{branchName}"
            ).ConfigureAwait(false);

            var counts = countStr.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var (aheadCount, behindCount) = counts.Length == 2
                ? (counts[0], counts[1])
                : (counts[0], "0");

            if (!int.TryParse(aheadCount, out var ahead) || !int.TryParse(behindCount, out var behind))
                return (0, 0);

            return (ahead, behind);
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error getting remote ahead/behind count in {repositoryPath}: {ex.Message}[/]");

            return (0, 0);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> FetchAsync(string repositoryPath, bool prune = false)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return false;

        try
        {
            var arguments = "fetch --all";

            if (prune)
                arguments += " --prune";

            await RunGitCommandAsync(repositoryPath, arguments).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error fetching updates in {repositoryPath}: {ex.Message}[/]");

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetLocalBranchesAsync(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return [];

        try
        {
            var result = await RunGitCommandAsync(repositoryPath, "branch --format='%(refname:short)'").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(result))
                return [];

            return [
                .. result.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static s => s.Replace("'", string.Empty, StringComparison.OrdinalIgnoreCase))
            ];
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error getting local branches in {repositoryPath}: {ex.Message}[/]");
            
            return [];
        }

    }

    /// <inheritdoc/>
    public async Task<bool> SynchronizeBranchAsync(string repositoryPath, string branchName)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return false;

        try
        {
            await RunGitCommandAsync(repositoryPath, $"fetch origin {branchName}").ConfigureAwait(false);
            await RunGitCommandAsync(repositoryPath, $"git branch --quiet --set-upstream-to=origin/{branchName} {branchName}").ConfigureAwait(false);
            await RunGitCommandAsync(repositoryPath, $"rebase origin/{branchName}").ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error synchronizing branch {branchName} in {repositoryPath}: {ex.Message}[/]");

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> StashAsync(string repositoryPath, bool includeUntracked = false)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return false;

        try
        {
            var arguments = "stash";

            if (includeUntracked)
                arguments += " --include-untracked";

            await RunGitCommandAsync(repositoryPath, arguments).ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error stashing changes in {repositoryPath}: {ex.Message}[/]");

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<GitRepositoryStatus> GetRepositoryStatusAsync(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return new GitRepositoryStatus(Path.GetFileName(repositoryPath), repositoryPath, null, false, [], "Repository does not exist.");

        try
        {
            var currentBranch = await RunGitCommandAsync(repositoryPath, "branch --show-current").ConfigureAwait(false);
            var remoteUrl = await RunGitCommandAsync(repositoryPath, "config --get remote.origin.url").ConfigureAwait(false);
            var branches = await GetLocalBranchesAsync(repositoryPath).ConfigureAwait(false);
            var hasUncommittedChanges = await HasUncommittedChangesAsync(repositoryPath).ConfigureAwait(false);

            if (branches.Count == 0)
                return new GitRepositoryStatus(Path.GetFileName(repositoryPath), repositoryPath, remoteUrl, false, [], "No local branches found.");

            var branchStatuses = new List<BranchStatus>();
            await FetchAsync(repositoryPath, true).ConfigureAwait(false);

            foreach (var branch in branches
                .Where(static b => !b.Contains("HEAD", StringComparison.OrdinalIgnoreCase) && !b.Contains("detached", StringComparison.OrdinalIgnoreCase)))
            {
                var isTracked = await IsBranchTrackedAsync(repositoryPath, branch).ConfigureAwait(false);

                var (aheadCount, behindCount) = isTracked
                    ? await GetRemoteAheadBehindCountAsync(repositoryPath, branch, fetch: false).ConfigureAwait(false)
                    : (0, 0);

                var isCurrentBranch = string.Equals(branch, currentBranch.Trim(), StringComparison.OrdinalIgnoreCase);

                branchStatuses.Add(new BranchStatus(branch, isTracked, aheadCount, behindCount, isCurrentBranch));
            }

            return new GitRepositoryStatus(Path.GetFileName(repositoryPath), repositoryPath, remoteUrl, hasUncommittedChanges, branchStatuses);
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error getting repository status for {repositoryPath}: {ex.Message}[/]");

            return new GitRepositoryStatus(Path.GetFileName(repositoryPath), repositoryPath, null, false, [], ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsBranchTrackedAsync(string repositoryPath, string branch)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return false;

        try
        {
            var result = await RunGitCommandAsync(repositoryPath, $"for-each-ref --format=\"%(upstream:short)\" refs/heads/{branch}").ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(result))
            {
                var refs = await RunGitCommandAsync(repositoryPath, $"show-ref origin/{branch}").ConfigureAwait(false);

                return !string.IsNullOrWhiteSpace(refs) && refs.Contains($"refs/remotes/origin/{branch}", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error checking if branch {branch} is tracked in {repositoryPath}: {ex.Message}[/]");
            return false;
        }
    }
}
