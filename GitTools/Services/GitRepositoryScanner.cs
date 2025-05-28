using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace GitTools.Services;

/// <summary>
/// Scans directories for git repositories and submodules.
/// </summary>
public sealed partial class GitRepositoryScanner(IAnsiConsole console, IFileSystem fileSystem) : IGitRepositoryScanner
{
    private const string GIT_DIR = ".git";
    private const string GIT_MODULES_FILE = ".gitmodules";

    /// <summary>
    /// Finds all Git repositories and submodules from the root folder.
    /// </summary>
    /// <param name="rootFolder">Root directory to scan.</param>
    public List<string> Scan(string rootFolder)
    {
        var gitRepos = new List<string>();
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SearchGitRepositories(rootFolder, gitRepos, processedPaths);

        return [.. gitRepos.Distinct()];
    }

    private void SearchGitRepositories
    (
        string rootFolder,
        List<string> gitRepos,
        HashSet<string> processedPaths
    )
    {
        var stack = new Stack<string>();
        stack.Push(rootFolder);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            ProcessDirectory(currentDir, gitRepos, processedPaths, stack);
        }
    }

    private void ProcessDirectory
    (
        string currentDir,
        List<string> gitRepos,
        HashSet<string> processedPaths,
        Stack<string> stack
    )
    {
        try
        {
            if (!processedPaths.Add(currentDir))
                return;

            if (IsGitRepository(currentDir))
            {
                lock (gitRepos)
                    gitRepos.Add(currentDir);

                AddSubmodules(currentDir, processedPaths, stack);
            }
            else
            {
                foreach (var dir in fileSystem.Directory.GetDirectories(currentDir))
                    stack.Push(dir);
            }
        }
        catch (Exception ex)
        {
            console.MarkupLineInterpolated($"[grey]Ignored: {currentDir} ({ex.Message})[/]");
        }
    }

    private bool IsGitRepository(string dir)
    {
        var gitDirPath = Path.Combine(dir, GIT_DIR);

        return fileSystem.Directory.Exists(gitDirPath) || fileSystem.File.Exists(gitDirPath);
    }

    private void AddSubmodules(string repoDir, HashSet<string> processedPaths, Stack<string> stack)
    {
        var gitmodulesFile = Path.Combine(repoDir, GIT_MODULES_FILE);

        if (!fileSystem.File.Exists(gitmodulesFile))
            return;

        try
        {
            var modulesContent = fileSystem.File.ReadAllText(gitmodulesFile);
            var pathRegex = GitPathMatcher();
            var matches = pathRegex.Matches(modulesContent);

            foreach (Match match in matches)
            {
                if (match.Success && match.Groups.Count > 1)
                {
                    var submodulePath = match.Groups[1].Value.Trim();
                    var fullSubmodulePath = Path.Combine(repoDir, submodulePath);

                    if (IsGitRepository(fullSubmodulePath) && !processedPaths.Contains(fullSubmodulePath))
                        stack.Push(fullSubmodulePath);
                }
            }
        }
        catch (Exception ex)
        {
            console.MarkupLineInterpolated($"[grey]Error processing submodules in: {repoDir} ({ex.Message})[/]");
        }
    }

    [GeneratedRegex(@"path\s*=\s*(.+)", RegexOptions.Multiline)]
    private static partial Regex GitPathMatcher();
}
