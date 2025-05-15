using Spectre.Console;

namespace GitTools;

/// <summary>
/// Command for removing tags from git repositories.
/// </summary>
public sealed class TagRemoveCommand
{
    private readonly GitRepositoryScanner _scanner = new();
    private readonly GitService _tagService = new();

    public async Task ExecuteAsync(string baseFolder, string[] tagsToSearch, bool removeRemote)
    {
        ShowInitialInfo(baseFolder, tagsToSearch);
        var allGitFolders = await _scanner.ScanAsync(baseFolder);

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
        await RemoveTagsFromRepositoriesAsync(selectedPaths, repoTagsMap, baseFolder, removeRemote);
        ShowFinalStatus(scanErrors, baseFolder);
    }

    private static void ShowInitialInfo(string baseFolder, string[] tagsToSearch)
    {
        AnsiConsole.MarkupLine($"[blue]Base folder: [bold]{baseFolder}[/][/]");
        AnsiConsole.MarkupLine($"[blue]Tags to search: [bold]{string.Join(", ", tagsToSearch)}[/][/]");
    }

    private async Task<(List<string> reposWithTag, Dictionary<string, List<string>> repoTagsMap, Dictionary<string, Exception> scanErrors)> FindRepositoriesWithTagsAsync(List<string> allGitFolders, string[] tagsToSearch)
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
                                if (await _tagService.HasTagAsync(repo, tag))
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

    private void ShowScanErrors(Dictionary<string, Exception> scanErrors, string baseFolder)
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

    private List<string> PromptRepositorySelection(Dictionary<string, List<string>> repoTagsMap, List<string> reposWithTag, string baseFolder, string[] tagsToSearch)
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

    private async Task RemoveTagsFromRepositoriesAsync(List<string> selectedPaths, Dictionary<string, List<string>> repoTagsMap, string baseFolder, bool removeRemote)
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
                    await _tagService.DeleteTagAsync(repo, tag);

                    if (removeRemote)
                    {
                        await _tagService.DeleteRemoteTagAsync(repo, tag);
                    }
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

    private void ShowFinalStatus(Dictionary<string, Exception> scanErrors, string baseFolder)
    {
        AnsiConsole.MarkupLine("[bold green]✅ Done![/]");
        ShowScanErrors(scanErrors, baseFolder);
    }

    private static string GetHierarchicalName(string repoPath, string baseFolder)
        => Path.GetRelativePath(baseFolder, repoPath).Replace(Path.DirectorySeparatorChar, '/');
}
