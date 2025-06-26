using System.CommandLine;
using GitTools.Services;
using Spectre.Console;

namespace GitTools.Commands;

/// <summary>
/// Command for removing tags from git repositories.
/// </summary>
public sealed class TagRemoveCommand : Command
{
    private readonly ITagSearchService _tagSearchService;
    private readonly ITagValidationService _tagValidationService;
    private readonly IConsoleDisplayService _consoleDisplayService;
    private readonly IGitService _gitService;
    private readonly IAnsiConsole _console;

    public TagRemoveCommand
    (
        ITagSearchService tagSearchService,
        ITagValidationService tagValidationService,
        IConsoleDisplayService consoleDisplayService,
        IGitService gitService,
        IAnsiConsole console
    )
        : base("rm", "Removes tags from git repositories.")
    {
        _tagSearchService = tagSearchService;
        _tagValidationService = tagValidationService;
        _consoleDisplayService = consoleDisplayService;
        _gitService = gitService;
        _console = console;

        var dirOption = new Argument<string>("directory")
        {
            Description = "Root directory of git repositories",
            Arity = ArgumentArity.ExactlyOne
        };

        var tagsOption = new Argument<string>("tags")
        {
            Description = "Tags to remove (comma separated)",
            Arity = ArgumentArity.ExactlyOne
        };

        var remoteOption = new Option<bool>("--remote", "-r")
        {
            Description = "Also remove the tag from the remote repository (if present)"
        };

        Arguments.Add(dirOption);
        Arguments.Add(tagsOption);
        Options.Add(remoteOption);

        SetAction
        (
            parseResult => ExecuteAsync
            (
                parseResult.GetValue(tagsOption)!,
                parseResult.GetValue(dirOption)!,
                parseResult.GetValue(remoteOption)
            )
        );
    }

    /// <summary>
    /// Executes the process of scanning Git repositories, identifying those with specific tags, and optionally removing
    /// the tags locally and remotely.
    /// </summary>
    /// <param name="tagsInput">A comma-separated list of tags to search for in the Git repositories.</param>
    /// <param name="baseFolder">The root folder to scan for Git repositories.</param>
    /// <param name="removeRemote">Whether the tags should also be removed from remote repositories.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ExecuteAsync(string tagsInput, string baseFolder, bool removeRemote)
    {
        var tags = _tagValidationService.ParseAndValidateTags(tagsInput);

        if (tags.Length == 0)
        {
            _console.MarkupLine("[red]No tags specified to remove.[/]");

            return;
        }

        _consoleDisplayService.ShowInitialInfo(baseFolder, tags);

        var searchResult = await _console.Progress()
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

                var progressCallback = new Action<string>(repoName =>
                {
                    task.Description($"Checking {repoName}...");
                    task.Increment(1);
                });

                var result = await _tagSearchService.SearchRepositoriesWithTagsAsync(baseFolder, tags, progressCallback).ConfigureAwait(false);
                task.StopTask();
                task.Description("Scanning completed.");

                return result;
            }).ConfigureAwait(false);

        if (searchResult.RepositoriesWithTags.Count == 0)
        {
            _console.MarkupLine("[yellow]No repository with the specified tag(s) found.[/]");
            _consoleDisplayService.ShowScanErrors(searchResult.ScanErrors, baseFolder);

            return;
        }

        var selectedPaths = PromptRepositorySelection(searchResult.RepositoryTagsMap, searchResult.RepositoriesWithTags, baseFolder, tags);

        if (selectedPaths.Count == 0)
        {
            _console.MarkupLine("[yellow]No repository selected.[/]");

            return;
        }

        _console.MarkupLine($"[blue]Removing tag(s) {string.Join(", ", tags)}...[/]");
        await RemoveTagsFromRepositoriesAsync(selectedPaths, searchResult.RepositoryTagsMap, baseFolder, removeRemote).ConfigureAwait(false);
        ShowFinalStatus(searchResult.ScanErrors, baseFolder);
    }

    /// <summary>
    /// Prompts the user to select repositories from a list of repositories that contain the specified tags.
    /// </summary>
    private List<string> PromptRepositorySelection(Dictionary<string, List<string>> repoTagsMap, List<string> reposWithTag, string baseFolder, string[] tagsToSearch)
    {
        var displayNameToPath = reposWithTag.ToDictionary
        (
            repo => $"{_consoleDisplayService.GetHierarchicalName(repo, baseFolder)} ({string.Join(", ", repoTagsMap[repo])})",
            static repo => repo
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

    /// <summary>
    /// Removes specified tags from the repositories in the provided list of paths.
    /// </summary>
    private async Task RemoveTagsFromRepositoriesAsync(List<string> selectedPaths, Dictionary<string, List<string>> repoTagsMap, string baseFolder, bool removeRemote)
    {
        foreach (var repo in selectedPaths)
        {
            var hierarchicalName = _consoleDisplayService.GetHierarchicalName(repo, baseFolder);
            var success = true;

            foreach (var tag in repoTagsMap[repo])
                success = await TryRemoveTagAsync(removeRemote, repo, hierarchicalName, success, tag).ConfigureAwait(false);

            if (success)
                _console.MarkupLineInterpolated($"[green]✅ {hierarchicalName}[/]");
        }
    }

    /// <summary>
    /// Tries to remove a tag from the specified repository.
    /// </summary>
    private async Task<bool> TryRemoveTagAsync(bool removeRemote, string repo, string hierarchicalName, bool success, string tag)
    {
        try
        {
            await _gitService.DeleteTagAsync(repo, tag).ConfigureAwait(false);

            if (removeRemote)
                await _gitService.DeleteRemoteTagAsync(repo, tag).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _console.MarkupLineInterpolated($"[red]❌ {hierarchicalName} (tag: {tag}): {ex.Message}[/]");
            success = false;
        }

        return success;
    }

    /// <summary>
    /// Shows the final status of the tag removal operation.
    /// </summary>
    private void ShowFinalStatus(Dictionary<string, Exception> scanErrors, string baseFolder)
    {
        _console.MarkupLine("[bold green]✅ Done![/]");
        _consoleDisplayService.ShowScanErrors(scanErrors, baseFolder);
    }
}
