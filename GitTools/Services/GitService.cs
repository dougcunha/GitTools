using System.Diagnostics;
using System.Globalization;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using GitTools.Extensions;
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

    private static readonly string[] _protectedBranches = 
    [
        "master",
        "main",
        "develop"
    ];

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
            : [.. result.SplitLines()];
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
            console.MarkupLineInterpolated($"[grey]{realWorkingDirectory}> git {arguments}[/]");

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

            var counts = countStr.SplitAndTrim('\t');

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
                    .. result.SplitLines()
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

            if (fetch)
                await FetchAsync(repositoryPath, true).ConfigureAwait(false);
            
            var branchStatuses = await GetBranchStatusesAsync(repositoryPath).ConfigureAwait(false);            

            return new GitRepositoryStatus
            (
                IGitService.GetRepositoryName(repositoryPath),
                hierarchicalName,
                repositoryPath,
                remoteUrl,
                hasUncommittedChanges,
                branchStatuses
            );
        }
        catch (Exception ex)
        {
            return new GitRepositoryStatus(IGitService.GetRepositoryName(repositoryPath), hierarchicalName, repositoryPath, null, false, [], ex.Message);
        }
    }

    /// <summary>
    /// Gets the list of branches that have been merged into the current branch.
    /// This method runs the `git branch --merged` command to retrieve the merged branches.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository.
    /// </param>
    /// <returns>
    /// An array of branch names that have been merged into the current branch.
    /// If the repository path is invalid or an error occurs, an empty array is returned.
    /// </returns>
    private async Task<string[]> GetMergedBranchesAsync(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return [];

        try
        {
            var output = await RunGitCommandAsync(repositoryPath, "branch --merged").ConfigureAwait(false);
            HashSet<string> branches = [];
            AddBranches(branches, output);

            return [.. branches];
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error getting merged branches in {repositoryPath}: {ex.Message}[/]");

            return [];
        }
    }

    /// <summary>
    /// Gets the status of all local branches in the specified repository.
    /// This method retrieves the list of local branches, checks if they are tracked, and gathers
    /// their upstream branches, current status, and ahead/behind counts.
    /// It also checks if the branches have been merged into the current branch.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository for which to get branch statuses.
    /// If the path is invalid or the repository does not exist, an empty list is returned
    /// </param>
    /// <returns>
    /// A list of <see cref="BranchStatus"/> objects representing the status of each local branch.
    /// Each <see cref="BranchStatus"/> contains information about the branch name, upstream branch,
    /// whether it is the current branch, how many commits it is ahead or behind the remote branch,
    /// and whether it has been merged into the current branch.
    /// </returns>
    private async Task<List<BranchStatus>> GetBranchStatusesAsync(string repositoryPath, bool excludeDetached = true)
    {
        var branches = await GetLocalBranchesAsync(repositoryPath).ConfigureAwait(false);
        var branchStatuses = new List<BranchStatus>();
        var mergedBranches = await GetMergedBranchesAsync(repositoryPath).ConfigureAwait(false);
        var goneBranches = await GetGoneBranchesAsync(repositoryPath).ConfigureAwait(false);

        foreach (var branch in branches)
        {
            if (excludeDetached && IsDetachedHead(branch))
                continue; // Skip detached HEAD branches

            var (isTracked, upstream) = IsBranchTrackedAsync(repositoryPath, branch).GetAwaiter().GetResult();
            var isCurrent = IsCurrentBranchAsync(repositoryPath, branch).GetAwaiter().GetResult();
            var isGone = goneBranches.Contains(branch, StringComparer.OrdinalIgnoreCase);
            var isFullyMerged = await IsFullyMergedAsync(repositoryPath, branch).ConfigureAwait(false);


            var (aheadCount, behindCount) = isTracked
                ? GetRemoteAheadBehindCountAsync(repositoryPath, branch, fetch: false).GetAwaiter().GetResult()
                : (0, 0);

            var isMerged = mergedBranches.Contains(branch, StringComparer.OrdinalIgnoreCase);
            var lastCommitDate = await GetLastCommitDateAsync(repositoryPath, branch).ConfigureAwait(false);

            branchStatuses.Add
            (
                new BranchStatus
                (
                    repositoryPath,
                    branch,
                    upstream,
                    isCurrent,
                    aheadCount,
                    behindCount,
                    isMerged,
                    isGone,
                    lastCommitDate,
                    isFullyMerged
                )
            );
        }

        return branchStatuses;
    }

    /// <summary>
    /// Checks if a branch is fully merged and can be safely deleted with 'git branch -d'.
    /// This method attempts to delete the branch with -d flag and checks if it would succeed.
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository.
    /// </param>
    /// <param name="branch">
    /// The name of the branch to check.
    /// </param>
    /// <returns>
    /// true if the branch is fully merged and can be safely deleted; otherwise, false.
    /// </returns>
    private async Task<bool> IsFullyMergedAsync(string repositoryPath, string branch)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return false;

        if (_protectedBranches.Contains(branch, StringComparer.OrdinalIgnoreCase))
            return false;

        try
        {
            // Use --dry-run to check if branch can be deleted without actually deleting it
            // This is safer than actually attempting the deletion
            var result = await RunGitCommandAsync(repositoryPath, $"branch -d --dry-run {branch}").ConfigureAwait(false);
            
            return true; // If no exception was thrown, the branch can be safely deleted
        }
        catch
        {
            // If the command fails, the branch is not fully merged
            return false;
        }
    }

    /// <summary>
    /// Gets the last commit date for a specific branch in the given repository.
    /// This method runs the `git log -1 --format=%cd` command to retrieve
    /// </summary>
    /// <param name="repositoryPath">
    /// The path to the git repository.
    /// </param>
    /// <param name="branch">
    /// The name of the branch for which to get the last commit date.
    /// </param>
    /// <returns>
    /// A <see cref="DateTime"/> representing the last commit date of the specified branch.
    /// </returns>
    private async Task<DateTime> GetLastCommitDateAsync(string repositoryPath, string branch)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return DateTime.MinValue;

        try
        {
            var output = await RunGitCommandAsync(repositoryPath, $"log -1 --format=%cd --date=format:\"%Y-%m-%d %H:%M:%S\" {branch} --").ConfigureAwait(false);

            return DateTime.ParseExact(output, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error getting last commit date for branch {branch} in {repositoryPath}: {ex.Message}[/]");

            return DateTime.MinValue;
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
    public async Task<List<BranchStatus>> GetPrunableBranchesAsync(string repositoryPath, bool merged, bool gone, int? olderThanDays)
    {
        var result = new List<BranchStatus>();

        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return [];

        // Return empty list if no criteria are provided
        if (!merged && !gone && !olderThanDays.HasValue)
            return [];

        try
        {
            var branches = (await GetBranchStatusesAsync(repositoryPath).ConfigureAwait(false))
                .Where(static b => !b.IsDetached && !b.IsCurrent && !_protectedBranches.Contains(b.Name, StringComparer.OrdinalIgnoreCase))
                .ToList(); 

            if (merged)
            {
                branches.Where(static b => b.IsMerged)
                    .ToList()
                    .ForEach(result.Add);
            }

            if (gone)
            {
                branches.Where(static b => b.IsGone)
                    .ToList()
                    .ForEach(n => result.Add(n));
            }

            if (olderThanDays.HasValue)
            {
                var threshold = DateTimeOffset.UtcNow.AddDays(-olderThanDays.Value);

                branches.Where(b => b.LastCommitDate < threshold)
                    .ToList()
                    .ForEach(n => result.Add(n));
            }

            return [.. result.DistinctBy(static b => b.Name.ToLowerInvariant())];
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error getting prunable branches in {repositoryPath}: {ex.Message}[/]");

            return [];
        }
    }

    private async Task<List<string>> GetGoneBranchesAsync(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath) || !fileSystem.Directory.Exists(repositoryPath))
            return [];

        try
        {
            var output = await RunGitCommandAsync(repositoryPath, "branch -vv").ConfigureAwait(false);

            List<string> goneBranches = [];

            foreach (var line in output.Split('\n'))
            {
                if (line.Contains(": gone]", StringComparison.OrdinalIgnoreCase))
                {
                    var tokens = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var branchName = tokens[0] == "*" ? tokens[1] : tokens[0];

                    goneBranches.Add(branchName);
                }
            }

            return goneBranches;
        }
        catch (Exception ex)
        {
            console.MarkupInterpolated($"[red]Error getting gone branches in {repositoryPath}: {ex.Message}[/]");

            return [];
        }
    }


    private static bool IsDetachedHead(string branch)
        => branch.Contains("HEAD", StringComparison.OrdinalIgnoreCase) ||
        branch.Contains("detached", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public Task DeleteLocalBranchAsync(string repositoryPath, string branch, bool force = false)
        => RunGitCommandAsync(repositoryPath, $"branch {(force ? "-D" : "-d")} {branch}");

    private static void AddBranches(HashSet<string> target, string output, bool ignoreDetached = true)
    {
        foreach (var line in output.SplitLines())
        {
            var branch = line.Replace("'", string.Empty).Trim('*', ' ');

            if (!ignoreDetached && IsDetachedHead(branch))
                continue;

            target.Add(branch);
        }
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
