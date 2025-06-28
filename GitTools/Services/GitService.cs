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
public sealed partial class GitService(IFileSystem fileSystem, IProcessRunner processRunner, IAnsiConsole console, GitToolsOptions options) : IGitService
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

        if (options.LogAllGitCommands)
            console.MarkupLineInterpolated($"[grey]{workingDirectory}> git {arguments}[/]");

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

    /// <inheritdoc/>
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

    /// <summary>
    /// Asynchronously retrieves the URL from a specified section in a Git configuration file.
    /// </summary>
    /// <remarks>This method reads the configuration file line by line and searches for the specified section.
    /// If the section is found, it attempts to extract the URL from the section's content. The method returns the first
    /// URL found in the specified section or <see langword="null"/> if no URL is present.</remarks>
    /// <param name="configPath">The path to the Git configuration file to read.</param>
    /// <param name="sectionName">The name of the section from which to retrieve the URL.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the URL as a string if found;
    /// otherwise, <see langword="null"/>.</returns>
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

            if (resultCode == 0)
                return !fileSystem.Directory.Exists(repositoryPath);

            console.MarkupInterpolated($"[red]Error deleting repository {repositoryPath}: result code {resultCode}[/]");

            return false;
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

            (string aheadCount, string behindCount) = counts.Length == 2
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
            var arguments = "fetch --all --tags";

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

            return string.IsNullOrWhiteSpace(result)
                ? []
                :
                [
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
    public async Task<bool> SynchronizeBranchAsync(BranchStatus branch, bool pushNewBranches = false)
    {
        if (string.IsNullOrWhiteSpace(branch.RepositoryPath) || !fileSystem.Directory.Exists(branch.RepositoryPath))
            return false;

        try
        {
            if (!branch.IsTracked)
            {
                if (!pushNewBranches)
                    return true; // If we don't want to send new branches, its ok.

                console.MarkupInterpolated($"[yellow]Pushing new branch {branch.Name} to remote...[/]");
                await RunGitCommandAsync(branch.RepositoryPath, $"push --set-upstream origin {branch.Name}").ConfigureAwait(false);

                return true;
            }

            await RunGitCommandAsync(branch.RepositoryPath, $"branch --quiet --set-upstream-to=origin/{branch.Name} {branch.Name}").ConfigureAwait(false);
            await RunGitCommandAsync(branch.RepositoryPath, $"checkout {branch.Name}").ConfigureAwait(false);
            await RunGitCommandAsync(branch.RepositoryPath, $"rebase --autostash origin/{branch.Name}").ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error synchronizing branch {branch.Name} in {branch.RepositoryPath}: {ex.Message}[/]");

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
    public async Task<bool> PopAsync(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return false;

        try
        {
            await RunGitCommandAsync(repositoryPath, "stash pop").ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error poping stashing changes in {repositoryPath}: {ex.Message}[/]");

            return false;
        }
    }

    /// <summary>
    /// Gets the hierarchical name of a repository based on its path and a base folder.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository.
    /// </param>
    /// <param name="baseFolder">
    /// The base folder to calculate the relative path from.
    /// </param>
    /// <returns>
    /// The hierarchical name of the repository, which is a relative path from the base folder.
    /// </returns>
    public static string GetHierarchicalName(string repositoryPath, string baseFolder)
    {
        repositoryPath = repositoryPath.Replace('\\', Path.DirectorySeparatorChar);
        baseFolder = baseFolder.Replace('\\', Path.DirectorySeparatorChar);

        var relativePath = Path.GetRelativePath(baseFolder, repositoryPath)
            .Replace(Path.DirectorySeparatorChar, '/');

        return relativePath.Length <= 1
            ? Path.GetFileName(repositoryPath)
            : relativePath;
    }

    /// <inheritdoc/>
    public async Task<GitRepositoryStatus> GetRepositoryStatusAsync(string repositoryPath, string rootDir, bool fetch = true)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
        {
            var safeName = string.IsNullOrWhiteSpace(repositoryPath) ? string.Empty : IGitService.GetRepositoryName(repositoryPath);
            var safeHierarchicalName = string.IsNullOrWhiteSpace(repositoryPath) ? string.Empty : GetHierarchicalName(repositoryPath, rootDir);

            return new GitRepositoryStatus(safeName, safeHierarchicalName, repositoryPath, null, false, [], "Repository does not exist.");
        }

        var hierarchicalName = GetHierarchicalName(repositoryPath, rootDir);

        try
        {
            var remoteUrl = await RunGitCommandAsync(repositoryPath, "config --get remote.origin.url").ConfigureAwait(false);
            var branches = await GetLocalBranchesAsync(repositoryPath).ConfigureAwait(false);
            var hasUncommittedChanges = await HasUncommittedChangesAsync(repositoryPath).ConfigureAwait(false);

            if (branches.Count == 0)
                return new GitRepositoryStatus(IGitService.GetRepositoryName(repositoryPath), hierarchicalName, repositoryPath, remoteUrl, false, [], "No local branches found.");

            List<BranchStatus> branchStatuses = [];

            if (fetch)
                await FetchAsync(repositoryPath, true).ConfigureAwait(false);

            foreach (var branch in branches
                .Where(static b => !b.Contains("HEAD", StringComparison.OrdinalIgnoreCase) && !b.Contains("detached", StringComparison.OrdinalIgnoreCase)))
            {
                var (isTracked, upstream) = await IsBranchTrackedAsync(repositoryPath, branch).ConfigureAwait(false);
                var isCurrent = await IsCurrentBranchAsync(repositoryPath, branch).ConfigureAwait(false);

                var (aheadCount, behindCount) = isTracked
                    ? await GetRemoteAheadBehindCountAsync(repositoryPath, branch, fetch: false).ConfigureAwait(false)
                    : (0, 0);

                branchStatuses.Add(new BranchStatus(repositoryPath, branch, upstream, isCurrent, aheadCount, behindCount));
            }

            return new GitRepositoryStatus(IGitService.GetRepositoryName(repositoryPath), hierarchicalName, repositoryPath, remoteUrl, hasUncommittedChanges, branchStatuses);
        }
        catch (Exception ex)
        {
            return new GitRepositoryStatus(IGitService.GetRepositoryName(repositoryPath), hierarchicalName, repositoryPath, null, false, [], ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsCurrentBranchAsync(string repositoryPath, string branch)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return false;

        try
        {
            var currentBranch = await RunGitCommandAsync(repositoryPath, "rev-parse --abbrev-ref HEAD").ConfigureAwait(false);

            return string.Equals(currentBranch.Trim(), branch, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error checking current branch in {repositoryPath}: {ex.Message}[/]");

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetCurrentBranchAsync(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return null;

        try
        {
            var currentBranch = await RunGitCommandAsync(repositoryPath, "rev-parse --abbrev-ref HEAD").ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(currentBranch) ? null : currentBranch.Trim();
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error retrieving current branch in {repositoryPath}: {ex.Message}[/]");

            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetPrunableBranchesAsync(string repositoryPath, bool merged, bool gone, int? olderThanDays)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return [];

        var protectedBranches = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "master",
            "main",
            "develop"
        };

        var current = await GetCurrentBranchAsync(repositoryPath).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(current))
            protectedBranches.Add(current);

        try
        {
            if (merged)
            {
                var mergedOutput = await RunGitCommandAsync(repositoryPath, "branch --merged --format='%(refname:short)'").ConfigureAwait(false);
                AddBranches(result, mergedOutput);
            }

            if (gone)
            {
                var goneOutput = await RunGitCommandAsync(repositoryPath, "branch -vv").ConfigureAwait(false);
                foreach (var line in goneOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!line.Contains("[gone]", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var branch = line.TrimStart('*', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                    result.Add(branch);
                }
            }

            if (olderThanDays.HasValue)
            {
                var threshold = DateTimeOffset.UtcNow.AddDays(-olderThanDays.Value);
                var dateOutput = await RunGitCommandAsync(repositoryPath, "for-each-ref --format='%(committerdate:iso8601)|%(refname:short)' refs/heads").ConfigureAwait(false);

                foreach (var line in dateOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var parts = line.Split('|', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    if (parts.Length != 2)
                        continue;

                    if (DateTimeOffset.TryParse(parts[0], out var commitDate) && commitDate < threshold)
                        result.Add(parts[1]);
                }
            }

            result.ExceptWith(protectedBranches);

            return [.. result];
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error getting prunable branches in {repositoryPath}: {ex.Message}[/]");

            return [];
        }
    }

    /// <inheritdoc/>
    public Task DeleteLocalBranchAsync(string repositoryPath, string branch, bool force = false)
        => RunGitCommandAsync(repositoryPath, $"branch {(force ? "-D" : "-d")} {branch}");

    private static void AddBranches(HashSet<string> target, string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            target.Add(line.Replace("'", string.Empty).Trim('*', ' '));
    }

    /// <inheritdoc/>
    public async Task<(bool isTracked, string? upstream)> IsBranchTrackedAsync(string repositoryPath, string branch)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return (false, null);

        try
        {
            var upstreamBranch = await RunGitCommandAsync(repositoryPath, $"for-each-ref --format=\"%(upstream:short)\" refs/heads/{branch}").ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(upstreamBranch))
                return (false, null);

            var refs = await RunGitCommandAsync(repositoryPath, $"show-ref origin/{branch}").ConfigureAwait(false);

            var isTracked = !string.IsNullOrWhiteSpace(refs)
                && refs.Contains($"refs/remotes/origin/{branch}", StringComparison.OrdinalIgnoreCase);

            return (isTracked, upstreamBranch.Trim());
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error checking if branch {branch} is tracked in {repositoryPath}: {ex.Message}[/]");

            return (false, null);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> PushAsync(string repositoryPath, string? branchName = null, bool force = false, bool tags = true)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return false;

        try
        {
            var arguments = new StringBuilder("push");

            if (force)
                arguments.Append(" --force");

            arguments.Append($" {branchName}");

            await RunGitCommandAsync(repositoryPath, arguments.ToString()).ConfigureAwait(false);

            if (tags)
                await RunGitCommandAsync(repositoryPath, "push --tags").ConfigureAwait(false);

            return true;
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error pushing changes in {repositoryPath}: {ex.Message}[/]");

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task CheckoutAsync(string repoRepoPath, string? repoCurrentBranch)
    {
        if (string.IsNullOrWhiteSpace(repoRepoPath) || !fileSystem.Directory.Exists(repoRepoPath) || string.IsNullOrWhiteSpace(repoCurrentBranch))
            return;

        try
        {
            var arguments = $"checkout {repoCurrentBranch}";

            await RunGitCommandAsync(repoRepoPath, arguments).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error checking out branch {repoCurrentBranch} in {repoRepoPath}: {ex.Message}[/]");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SynchronizeRepositoryAsync
    (
        GitRepositoryStatus repo,
        Action<FormattableString>? progress,
        bool withUncommited = false,
        bool pushNewBranches = false
    )
    {
        if (string.IsNullOrWhiteSpace(repo.RepoPath) || !fileSystem.Directory.Exists(repo.RepoPath))
            return false;

        try
        {
            var hasStash = false;

            if (withUncommited && repo.HasUncommitedChanges)
            {
                progress?.Invoke($"[yellow]⚠[/] [grey]{repo.HierarchicalName} has uncommitted changes. Stashing them.[/]");
                hasStash = await StashAsync(repo.RepoPath, includeUntracked: true).ConfigureAwait(false);
            }

            if (repo.LocalBranches.Count == 0)
            {
                progress?.Invoke($"[red]✗[/] [grey]{repo.HierarchicalName} has no local branches. Skipping.[/]");

                return false;
            }

            var allBranchesOk = await SynchronizeAllBranchesAsync(repo, progress, pushNewBranches).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(repo.CurrentBranch))
                await CheckoutAsync(repo.RepoPath, repo.CurrentBranch).ConfigureAwait(false);

            if (hasStash)
                await PopAsync(repo.RepoPath).ConfigureAwait(false);

            await PushAsync(repo.RepoPath).ConfigureAwait(false);

            if (!allBranchesOk)
                return false;

            progress?.Invoke($"[green]✓[/] [grey]{repo.HierarchicalName} updated.[/]");

            return true;
        }
        catch (Exception ex)
        {
            progress?.Invoke($"[red]✗[/] [grey]{repo.HierarchicalName} sync error: {ex.Message}[/]");

            return false;
        }
    }

    /// <summary>
    /// Synchronizes all branches of a given repository.
    /// </summary>
    /// <param name="repo">
    /// The repository to synchronize.
    /// </param>
    /// <param name="progress">
    /// An optional action to report progress messages.
    /// </param>
    /// <param name="pushNewBranches">
    /// true to push new branches to the remote repository; otherwise, false.
    /// </param>
    /// <returns>
    /// true if all branches were successfully synchronized; otherwise, false.
    /// </returns>
    private async Task<bool> SynchronizeAllBranchesAsync(GitRepositoryStatus repo, Action<FormattableString>? progress, bool pushNewBranches)
    {
        var allBranchesOk = true;

        foreach (var branch in repo.LocalBranches)
        {
            var branchOk = await SynchronizeBranchAsync(branch, pushNewBranches).ConfigureAwait(false);

            if (branchOk)
                continue;

            progress?.Invoke($"[red]✗[/] [grey]{repo.HierarchicalName} failed to synchronize branch {branch.Name}.[/]");
            allBranchesOk = false;
        }

        return allBranchesOk;
    }
}
