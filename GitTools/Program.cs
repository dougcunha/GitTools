using Spectre.Console;
using FluentArgs;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace GitTools;

/// <summary>
/// Tool for searching and removing tags in Git repositories and submodules.
/// </summary>
public sealed class Program
{
    private const string GIT_DIR = ".git";
    private const string GIT_MODULES_FILE = ".gitmodules";

    /// <summary>
    /// Main entry point.
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    public static async Task Main(string[] args)
    {
        await FluentArgsBuilder.New()
            .WithApplicationDescription("GitTool - A tool for searching and removing tags in Git repositories.")
            .RegisterHelpFlag("-h", "--help", "/help")
            .Parameter<string>("-d", "--dir", "/dir")
                .WithDescription("Root directory of git repositories.")
                .WithExamples("C:\\GitRepos", "/home/user/git")
                .WithValidation(Directory.Exists, "Directory does not exist.")                
                .IsRequired()
            .Parameter<string>("-t", "--tags", "/tags")
                .WithDescription("Comma-separated list of tags to search.")
                .WithExamples("NET8, NET7")
                .IsRequired()
            .Call(tags => dir =>
            {
                return MainImpl(tags, dir);
            })
            .ParseAsync(args);
    }

    private static async Task MainImpl(string tags, string dir)
    {
        var baseFolder = dir;
        var tagsToSearch = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        ShowInitialInfo(baseFolder, tagsToSearch);

        var allGitFolders = await ScanGitRepositoriesAsync(baseFolder);

        if (allGitFolders.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No Git repositories found.[/]");

            return;
        }

        AnsiConsole.MarkupLine($"[blue]{allGitFolders.Count} repositories found.[/]");

        var (reposWithTag, repoTagsMap, scanErrors) = await FindRepositoriesWithTagsAsync(allGitFolders, tagsToSearch);

        if (reposWithTag.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No repository with the specified tag(s) found.[/]");
            ShowScanErrors(scanErrors, baseFolder);

            return;
        }

        var selectedPaths = PromptRepositorySelection(repoTagsMap, reposWithTag, baseFolder, tagsToSearch);

        if (selectedPaths.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No repository selected.[/]");

            return;
        }

        AnsiConsole.MarkupLine($"[blue]Removing tag(s) {string.Join(", ", tagsToSearch)}...[/]");
        await RemoveTagsFromRepositoriesAsync(selectedPaths, repoTagsMap, baseFolder);
        ShowFinalStatus(scanErrors, baseFolder);
    }

    private static void ShowInitialInfo(string baseFolder, string[] tagsToSearch)
    {
        AnsiConsole.MarkupLine($"[blue]Base folder: [bold]{baseFolder}[/][/]");
        AnsiConsole.MarkupLine($"[blue]Tags to search: [bold]{string.Join(", ", tagsToSearch)}[/][/]");
    }

    private static async Task<(List<string> reposWithTag, Dictionary<string, List<string>> repoTagsMap, Dictionary<string, Exception> scanErrors)> FindRepositoriesWithTagsAsync(List<string> allGitFolders, string[] tagsToSearch)
    {
        var reposWithTag = new List<string>();
        var repoTagsMap = new Dictionary<string, List<string>>();
        var scanErrors = new Dictionary<string, Exception>();

        await AnsiConsole.Status()
            .StartAsync($"🔍 Checking tags '{string.Join(", ", tagsToSearch)}' in repositories...",
            async ctx =>
            {
                foreach (var repo in allGitFolders)
                {
                    ctx.Status($"Checking {Path.GetFileName(repo)}...");
                    ctx.Spinner(Spinner.Known.Line);

                    try
                    {
                        var foundTags = new List<string>();

                        foreach (var tag in tagsToSearch)
                        {
                            try
                            {
                                if (await HasTagAsync(repo, tag))
                                {
                                    foundTags.Add(tag);
                                }
                            }
                            catch (Exception ex)
                            {
                                scanErrors[repo] = ex;

                                break;
                            }
                        }
                        if (foundTags.Count > 0)
                        {
                            reposWithTag.Add(repo);
                            repoTagsMap[repo] = foundTags;
                        }
                    }
                    catch (Exception ex)
                    {
                        scanErrors[repo] = ex;
                    }
                }
            });

        return (reposWithTag, repoTagsMap, scanErrors);
    }

    private static void ShowScanErrors(Dictionary<string, Exception> scanErrors, string baseFolder)
    {
        if (scanErrors.Count == 0)
            return;

        if (!AnsiConsole.Confirm($"[red]{scanErrors.Count} scan errors detected.Do you want to see the details?[/]"))
            return;

        foreach (var err in scanErrors)
        {
            var rule = new Rule($"[red]{GetHierarchicalName(err.Key, baseFolder)}[/]")
                .RuleStyle(new Style(Color.Red))                
                .Centered();

            AnsiConsole.Write(rule);                        
            AnsiConsole.WriteException(err.Value, ExceptionFormats.ShortenEverything);
        }
    }

    private static List<string> PromptRepositorySelection(Dictionary<string, List<string>> repoTagsMap, List<string> reposWithTag, string baseFolder, string[] tagsToSearch)
    {
        var displayNameToPath = reposWithTag.ToDictionary
        (
            repo => $"{GetHierarchicalName(repo, baseFolder)} ({string.Join(", ", repoTagsMap[repo])})",
            repo => repo
        );

        var selected = AnsiConsole.Prompt
        (
            new MultiSelectionPrompt<string>()
                .Title($"[green]Select the repositories to remove the tag(s) [bold]{string.Join(", ", tagsToSearch)}[/]:[/]")
                .NotRequired()
                .PageSize(20)
                .MoreChoicesText("[grey](Use space to select, enter to confirm)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to select, [green]<enter>[/] to confirm)[/]")
                .AddChoices(displayNameToPath.Keys)
        );

        return [.. selected.Select(display => displayNameToPath[display])];
    }

    private static async Task RemoveTagsFromRepositoriesAsync(List<string> selectedPaths, Dictionary<string, List<string>> repoTagsMap, string baseFolder)
    {
        foreach (var repo in selectedPaths)
        {
            var hierarchicalName = GetHierarchicalName(repo, baseFolder);
            var success = true;
            var foundTags = repoTagsMap[repo];

            foreach (var tag in foundTags)
            {
                try
                {
                    await DeleteTagAsync(repo, tag);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]❌ {hierarchicalName} (tag: {tag}): {ex.Message}[/]");
                    success = false;
                }
            }

            if (success)
                AnsiConsole.MarkupLine($"[green]✅ {hierarchicalName}[/]");
        }
    }

    private static void ShowFinalStatus(Dictionary<string, Exception> scanErrors, string baseFolder)
    {
        AnsiConsole.MarkupLine("[bold green]✅ Done![/]");
        ShowScanErrors(scanErrors, baseFolder);
    }

    /// <summary>
    /// Returns the real Git directory (handles submodules with .git as a file).
    /// </summary>
    private static string GetRealGitDirectory(string repoPath)
    {
        var gitPath = Path.Combine(repoPath, GIT_DIR);

        if (Directory.Exists(gitPath))
            return repoPath;

        if (File.Exists(gitPath))
        {
            var content = File.ReadAllText(gitPath).Trim();

            if (content.StartsWith("gitdir:"))
            {
                var relativePath = content[7..].Trim();
                var fullPath = Path.GetFullPath(Path.Combine(repoPath, relativePath));

                return fullPath;
            }
        }

        return repoPath;
    }

    /// <summary>
    /// Returns the hierarchical name of the repository (e.g., repository/submodule).
    /// </summary>
    private static string GetHierarchicalName(string repoPath, string baseFolder)
        => Path.GetRelativePath(baseFolder, repoPath).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>
    /// Finds all Git repositories and submodules from the root folder.
    /// </summary>
    public static async Task<List<string>> ScanGitRepositoriesAsync(string rootFolder)
    {
        var gitRepos = new List<string>();
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await AnsiConsole.Status()
            .StartAsync("⏳ Scanning Git repositories...", async ctx =>
            {
                await Task.Run(() =>
                {
                    SearchGitRepositories(rootFolder, gitRepos, processedPaths, ctx);
                });
            });

        return [.. gitRepos.Distinct()];
    }

    private static void SearchGitRepositories(string rootFolder, List<string> gitRepos, HashSet<string> processedPaths, StatusContext ctx)
    {
        var stack = new Stack<string>();
        stack.Push(rootFolder);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            ProcessDirectory(currentDir, gitRepos, processedPaths, stack, ctx);
        }
    }

    private static void ProcessDirectory(string currentDir, List<string> gitRepos, HashSet<string> processedPaths, Stack<string> stack, StatusContext ctx)
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
            AnsiConsole.MarkupLine($"[grey]Ignored: {currentDir} ({ex.Message})[/]");
        }

        ctx.Status($"Checking {Path.GetFileName(currentDir)}...");
    }

    private static bool IsGitRepository(string dir)
    {
        var gitDirPath = Path.Combine(dir, GIT_DIR);
        
        return Directory.Exists(gitDirPath) || File.Exists(gitDirPath);
    }

    private static void AddSubmodules(string repoDir, HashSet<string> processedPaths, Stack<string> stack)
    {
        var gitmodulesFile = Path.Combine(repoDir, GIT_MODULES_FILE);

        if (!File.Exists(gitmodulesFile))
            return;

        try
        {
            var modulesContent = File.ReadAllText(gitmodulesFile);
            var pathRegex = new Regex(@"path\s*=\s*(.+)", RegexOptions.Multiline);
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
            AnsiConsole.MarkupLine($"[grey]Error processing submodules in: {repoDir} ({ex.Message})[/]");
        }
    }

    /// <summary>
    /// Checks if the repository has the given tag.
    /// </summary>
    public static async Task<bool> HasTagAsync(string repoPath, string tag)
    {
        var result = await RunGitCommandAsync(repoPath, $"tag -l {tag}");

        return !string.IsNullOrWhiteSpace(result);
    }

    /// <summary>
    /// Removes the given tag from the repository.
    /// </summary>
    public static async Task DeleteTagAsync(string repoPath, string tag)
        => await RunGitCommandAsync(repoPath, $"tag -d {tag}");

    /// <summary>
    /// Runs a git command in the correct repository directory.
    /// </summary>
    public static async Task<string> RunGitCommandAsync(string workingDirectory, string arguments)
    {
        var realWorkingDirectory = GetRealGitDirectory(workingDirectory);

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = realWorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception(error.ToString());

        return output.ToString();
    }
}
