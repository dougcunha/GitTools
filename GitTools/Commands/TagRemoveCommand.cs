using System.CommandLine;
using GitTools.Services;
using Spectre.Console;

namespace GitTools.Commands;

/// <summary>
/// Command for removing tags from git repositories.
/// </summary>
public sealed class TagRemoveCommand : Command
{
    private readonly IGitRepositoryScanner _gitScanner;
    private readonly IGitService _tagService;
    private readonly IAnsiConsole _console;

    public TagRemoveCommand
    (
        IGitRepositoryScanner gitScanner,
        IGitService gitService,
        IAnsiConsole console
    )
        : base("rm", "Removes tags from git repositories.")
    {
        _gitScanner = gitScanner;
        _tagService = gitService;
        _console = console;

        var dirOption = new Argument<string>("directory", "Root directory of git repositories");

        var tagsOption = new Argument<string>("tags", "Tags to remove (comma separated)")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var remoteOption = new Option<bool>(["--remote", "-r"], "Also remove the tag from the remote repository (if present)");

        AddArgument(dirOption);
        AddArgument(tagsOption);
        AddOption(remoteOption);

        this.SetHandler
        (
            ExecuteAsync,
            tagsOption,
            dirOption,
            remoteOption
        );
    }

    public async Task ExecuteAsync(string tagsToSearch, string baseFolder, bool removeRemote)
    {
        var tags = tagsToSearch.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tags.Length == 0)
        {
            _console.MarkupLine("[red]No tags specified to remove.[/]");

            return;
        }

        ShowInitialInfo(baseFolder, tags);
        var allGitFolders = _gitScanner.Scan(baseFolder);

        if (allGitFolders.Count == 0)
        {
            _console.MarkupLine("[red]No Git repositories found.[/]");

            return;
        }

        _console.MarkupLine($"[blue]{allGitFolders.Count} repositories found.[/]");

        var (reposWithTag, repoTagsMap, scanErrors) = await FindRepositoriesWithTagsAsync(allGitFolders, tags).ConfigureAwait(false);

        if (reposWithTag.Count == 0)
        {
            _console.MarkupLine($"[yellow]No repository with the specified tag(s) found.[/]");
            ShowScanErrors(scanErrors, baseFolder);

            return;
        }

        var selectedPaths = PromptRepositorySelection(repoTagsMap, reposWithTag, baseFolder, tags);
        if (selectedPaths.Count == 0)
        {
            _console.MarkupLine("[yellow]No repository selected.[/]");

            return;
        }

        _console.MarkupLine($"[blue]Removing tag(s) {string.Join(", ", tags)}...[/]");
        await RemoveTagsFromRepositoriesAsync(selectedPaths, repoTagsMap, baseFolder, removeRemote).ConfigureAwait(false);
        ShowFinalStatus(scanErrors, baseFolder);
    }

    private void ShowInitialInfo(string baseFolder, string[] tagsToSearch)
    {
        _console.MarkupLineInterpolated($"[blue]Base folder: [bold]{baseFolder}[/][/]");
        _console.MarkupLineInterpolated($"[blue]Tags to search: [bold]{string.Join(", ", tagsToSearch)}[/][/]");
    }

    private async Task<(List<string> reposWithTag, Dictionary<string, List<string>> repoTagsMap, Dictionary<string, Exception> scanErrors)> FindRepositoriesWithTagsAsync(List<string> allGitFolders, string[] tagsToSearch)
    {
        var reposWithTag = new List<string>();
        var repoTagsMap = new Dictionary<string, List<string>>();
        var scanErrors = new Dictionary<string, Exception>();

        await _console.Progress()
            .Columns
            (
                new ProgressBarColumn
                {
                    CompletedStyle = new Style(foreground: Color.Green1, decoration: Decoration.Conceal | Decoration.Bold | Decoration.Invert),
                    RemainingStyle = new Style(decoration: Decoration.Conceal),
                    FinishedStyle = new Style(foreground: Color.Green1, decoration: Decoration.Conceal | Decoration.Bold | Decoration.Invert)
                },
                new PercentageColumn(),
                new SpinnerColumn(),
                new ElapsedTimeColumn(),
                new TaskDescriptionColumn()
            )
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Scanning repositories for tags...");
                task.MaxValue = allGitFolders.Count;

                foreach (var repo in allGitFolders)
                {
                    task.Description($"Checking {Path.GetFileName(repo)}...");
                    task.Increment(1);

                    try
                    {
                        var foundTags = new List<string>();

                        foreach (var tag in tagsToSearch)
                        {
                            try
                            {
                                if (await _tagService.HasTagAsync(repo, tag).ConfigureAwait(false))
                                    foundTags.Add(tag);
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

                task.StopTask();
                task.Description("Scanning completed.");
            });

        return (reposWithTag, repoTagsMap, scanErrors);
    }

    private void ShowScanErrors(Dictionary<string, Exception> scanErrors, string baseFolder)
    {
        if (scanErrors.Count == 0)
            return;

        if (!_console.Confirm($"[red]{scanErrors.Count} scan errors detected.Do you want to see the details?[/]"))
            return;

        foreach (var err in scanErrors)
        {
            var rule = new Rule($"[red]{GetHierarchicalName(err.Key, baseFolder)}[/]")
                .RuleStyle(new Style(Color.Red))
                .Centered();

            _console.Write(rule);
            _console.WriteException(err.Value, ExceptionFormats.ShortenEverything);
        }
    }

    private List<string> PromptRepositorySelection(Dictionary<string, List<string>> repoTagsMap, List<string> reposWithTag, string baseFolder, string[] tagsToSearch)
    {
        var displayNameToPath = reposWithTag.ToDictionary
        (
            repo => $"{GetHierarchicalName(repo, baseFolder)} ({string.Join(", ", repoTagsMap[repo])})",
            repo => repo
        );

        var selected = _console.Prompt
        (
            new MultiSelectionPrompt<string>()
                .Title($"[green]Select the repositories to remove the tag(s) [bold]{string.Join(", ", tagsToSearch)}[/]:[/]")
                .NotRequired()
                .PageSize(20)
                .MoreChoicesText("[grey](Use space to select, enter to confirm)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to select, [green]<enter>[/] to confirm)[/]")
                .AddChoiceGroup("Select all", displayNameToPath.Keys)
        );

        return [.. selected.Select(display => displayNameToPath[display])];
    }

    private async Task RemoveTagsFromRepositoriesAsync
    (
        List<string> selectedPaths,
        Dictionary<string, List<string>> repoTagsMap,
        string baseFolder,
        bool removeRemote
    )
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
                    await _tagService.DeleteTagAsync(repo, tag).ConfigureAwait(false);

                    if (removeRemote)
                    {
                        await _tagService.DeleteRemoteTagAsync(repo, tag).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _console.MarkupLineInterpolated($"[red]❌ {hierarchicalName} (tag: {tag}): {ex.Message}[/]");
                    success = false;
                }
            }

            if (success)
                _console.MarkupLineInterpolated($"[green]✅ {hierarchicalName}[/]");
        }
    }

    private void ShowFinalStatus(Dictionary<string, Exception> scanErrors, string baseFolder)
    {
        _console.MarkupLine("[bold green]✅ Done![/]");
        ShowScanErrors(scanErrors, baseFolder);
    }

    private static string GetHierarchicalName(string repoPath, string baseFolder)
    {
        var relativePath = Path.GetRelativePath(baseFolder, repoPath).Replace(Path.DirectorySeparatorChar, '/');

        return relativePath.Length <= 1 // If the path is just the root or empty, return the repo name
            ? Path.GetFileName(repoPath)
            : relativePath;
    }
}
