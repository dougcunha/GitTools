using System.IO.Abstractions;
using System.Text.RegularExpressions;
using GitTools.Models;
using GitTools.Utils;
using Spectre.Console;

namespace GitTools.Services;

/// <summary>
/// Scans directories for git repositories and submodules.
/// </summary>
public sealed partial class GitRepositoryScanner
(
    IAnsiConsole console,
    IFileSystem fileSystem,
    GitToolsOptions options
) : IGitRepositoryScanner
{
    private const string GIT_DIR = ".git";
    private const string GIT_MODULES_FILE = ".gitmodules";

    /// <inheritdoc/>
    public List<string> Scan(string rootFolder)
    {
        var gitRepos = new List<string>();
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SearchGitRepositories(rootFolder, gitRepos, processedPaths, options.IncludeSubmodules);

        var repositories = gitRepos.Distinct().ToList();

        if (options.HasRepositoryFilters)
            repositories = ApplyRepositoryFiltering(repositories, rootFolder);

        return repositories;
    }

    /// <summary>
    /// Searches for git repositories starting from the root folder.
    /// </summary>
    /// <param name="rootFolder">
    /// The root directory to start the search from.
    /// </param>
    /// <param name="gitRepos">
    /// The list to store found git repositories.
    /// </param>
    /// <param name="processedPaths">
    /// The set of already processed paths to avoid duplicates.
    /// </param>
    /// <param name="includeSubmodules">
    /// true to include submodules in the search, false to ignore them.
    /// </param>
    private void SearchGitRepositories
    (
        string rootFolder,
        List<string> gitRepos,
        HashSet<string> processedPaths,
        bool includeSubmodules = true
    )
    {
        var stack = new Stack<string>();
        stack.Push(rootFolder);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            ProcessDirectory(currentDir, gitRepos, processedPaths, stack, includeSubmodules);
        }
    }

    /// <summary>
    /// Processes the current directory to check if it is a git repository.
    /// </summary>
    /// <param name="currentDir">
    /// The current directory to process.
    /// </param>
    /// <param name="gitRepos">
    /// The list to store found git repositories.
    /// </param>
    /// <param name="processedPaths">
    /// The set of already processed paths to avoid duplicates.
    /// </param>
    /// <param name="pendingDirs">
    /// The directories to keep track of directories to process.
    /// </param>
    /// <param name="includeSubmodules">
    /// true to include submodules in the search, false to ignore them.
    /// </param>
    private void ProcessDirectory
    (
        string currentDir,
        List<string> gitRepos,
        HashSet<string> processedPaths,
        Stack<string> pendingDirs,
        bool includeSubmodules
    )
    {
        try
        {
            if (!processedPaths.Add(currentDir))
                return;

            if (IsGitRepository(currentDir))
            {
                lock (gitRepos)
                {
                    gitRepos.Add(currentDir);
                }

                if (includeSubmodules)
                    AddSubmodules(currentDir, processedPaths, pendingDirs);
            }
            else
            {
                foreach (var dir in fileSystem.Directory.GetDirectories(currentDir))
                    pendingDirs.Push(dir);
            }
        }
        catch (Exception ex)
        {
            console.MarkupLineInterpolated($"[grey]Ignored: {currentDir} ({ex.Message})[/]");
        }
    }

    /// <summary>
    /// Checks if the specified directory is a git repository.
    /// </summary>
    /// <param name="dir">
    /// The directory to check.
    /// </param>
    /// <returns>
    /// True if the directory is a git repository, otherwise false.
    /// </returns>
    private bool IsGitRepository(string dir)
    {
        var gitDirPath = Path.Combine(dir, GIT_DIR);

        return fileSystem.Directory.Exists(gitDirPath) || fileSystem.File.Exists(gitDirPath);
    }

    /// <summary>
    /// Adds submodules found in the specified repository directory to the pendingDirs for further processing.
    /// </summary>
    /// <param name="repoDir">
    /// The directory of the git repository to check for submodules.
    /// </param>
    /// <param name="processedPaths">
    /// The set of already processed paths to avoid duplicates.
    /// </param>
    /// <param name="pendingDirs">
    /// The stack of directories to keep track of directories to process.
    /// </param>
    private void AddSubmodules(string repoDir, HashSet<string> processedPaths, Stack<string> pendingDirs)
    {
        var gitmodulesFile = Path.Combine(repoDir, GIT_MODULES_FILE);

        if (!fileSystem.File.Exists(gitmodulesFile))
            return;

        try
        {
            var modulesContent = fileSystem.File.ReadAllText(gitmodulesFile);
            var pathRegex = GitPathMatcher();

            foreach (var match in pathRegex.Matches(modulesContent)
                .Where(static m => m is { Success: true, Groups.Count: > 1 }))
            {
                var submodulePath = match.Groups[1].Value.Trim();
                var fullSubmodulePath = Path.Combine(repoDir, submodulePath);

                if (IsGitRepository(fullSubmodulePath) && !processedPaths.Contains(fullSubmodulePath))
                    pendingDirs.Push(fullSubmodulePath);
            }
        }
        catch (Exception ex)
        {
            console.MarkupLineInterpolated($"[grey]Error processing submodules in: {repoDir} ({ex.Message})[/]");
        }
    }

    /// <summary>
    /// Applies repository filtering based on the configured patterns.
    /// </summary>
    /// <param name="repositories">The list of repository paths to filter.</param>
    /// <param name="rootFolder">The root folder used for relative path calculation.</param>
    /// <returns>The filtered list of repositories.</returns>
    private List<string> ApplyRepositoryFiltering(List<string> repositories, string rootFolder)
    {
        var filteredRepositories = new List<string>();

        foreach (var filter in options.RepositoryFilters)
        {
            var repositoryNames = repositories.Select(path => GetRepositoryName(path, rootFolder));
            var matchedNames = WildcardMatcher.MatchItems(repositoryNames, filter);

            var matchedPaths = repositories.Where(path => matchedNames.Contains(GetRepositoryName(path, rootFolder)));

            filteredRepositories.AddRange(matchedPaths);
        }

        var result = filteredRepositories.Distinct().ToList();

        if (result.Count != repositories.Count)
        {
            console.MarkupLineInterpolated($"[blue]Repository filtering applied: {result.Count} of {repositories.Count} repositories match the specified patterns.[/]");
        }

        return result;
    }

    /// <summary>
    /// Gets the repository name for filtering purposes.
    /// </summary>
    /// <param name="repositoryPath">The full path to the repository.</param>
    /// <param name="rootFolder">The root folder used for relative path calculation.</param>
    /// <returns>The repository name used for filtering.</returns>
    private static string GetRepositoryName(string repositoryPath, string rootFolder)
    {
        var relativePath = Path.GetRelativePath(rootFolder, repositoryPath);

        return relativePath.Replace('\\', '/');
    }

    /// <summary>
    /// Regex to match the path of submodules in the .gitmodules file.
    /// </summary>
    [GeneratedRegex(@"path\s*=\s*(.+)", RegexOptions.Multiline)]
    private static partial Regex GitPathMatcher();
}
