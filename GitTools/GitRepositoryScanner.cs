namespace GitTools;

/// <summary>
/// Scans directories for git repositories and submodules.
/// </summary>
public sealed class GitRepositoryScanner
{
    private const string GIT_DIR = ".git";
    private const string GIT_MODULES_FILE = ".gitmodules";

    /// <summary>
    /// Finds all Git repositories and submodules from the root folder.
    /// </summary>
    /// <param name="rootFolder">Root directory to scan.</param>
    public async Task<List<string>> ScanAsync(string rootFolder)
    {
        var gitRepos = new List<string>();
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await Spectre.Console.AnsiConsole.Status()
            .StartAsync("⏳ Scanning Git repositories...", async ctx =>
            {
                await Task.Run(() =>
                {
                    SearchGitRepositories(rootFolder, gitRepos, processedPaths, ctx);
                });
            });

        return [.. gitRepos.Distinct()];
    }

    private void SearchGitRepositories(string rootFolder, List<string> gitRepos, HashSet<string> processedPaths, Spectre.Console.StatusContext ctx)
    {
        var stack = new Stack<string>();
        stack.Push(rootFolder);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            ProcessDirectory(currentDir, gitRepos, processedPaths, stack, ctx);
        }
    }

    private void ProcessDirectory(string currentDir, List<string> gitRepos, HashSet<string> processedPaths, Stack<string> stack, Spectre.Console.StatusContext ctx)
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
                foreach (var dir in Directory.GetDirectories(currentDir))
                    stack.Push(dir);
            }
        }
        catch (Exception ex)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[grey]Ignored: {currentDir} ({ex.Message})[/]");
        }

        ctx.Status = $"Checking {Path.GetFileName(currentDir)}...";
    }

    private bool IsGitRepository(string dir)
    {
        var gitDirPath = Path.Combine(dir, GIT_DIR);
        return Directory.Exists(gitDirPath) || File.Exists(gitDirPath);
    }

    private void AddSubmodules(string repoDir, HashSet<string> processedPaths, Stack<string> stack)
    {
        var gitmodulesFile = Path.Combine(repoDir, GIT_MODULES_FILE);

        if (!File.Exists(gitmodulesFile))
            return;

        try
        {
            var modulesContent = File.ReadAllText(gitmodulesFile);
            var pathRegex = new System.Text.RegularExpressions.Regex(@"path\s*=\s*(.+)", System.Text.RegularExpressions.RegexOptions.Multiline);
            var matches = pathRegex.Matches(modulesContent);

            foreach (System.Text.RegularExpressions.Match match in matches)
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
            Spectre.Console.AnsiConsole.MarkupLine($"[grey]Error processing submodules in: {repoDir} ({ex.Message})[/]");
        }
    }
}
