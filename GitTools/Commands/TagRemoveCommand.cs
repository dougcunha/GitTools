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

    /// <summary>
    /// Executes the process of scanning Git repositories, identifying those with specific tags, and optionally removing
    /// the tags locally and remotely.
    /// </summary>
    /// <remarks>This method performs the following steps: <list type="number"> <item>Scans the specified base
    /// folder for Git repositories.</item> <item>Identifies repositories containing the specified tags.</item>
    /// <item>Prompts the user to select repositories for tag removal.</item> <item>Removes the specified tags from the
    /// selected repositories, optionally including remote removal.</item> </list> If no tags are specified, no action
    /// is taken. If no repositories are found or selected, the process terminates early.</remarks>
    /// <param name="tagsToSearch">A comma-separated list of tags to search for in the Git repositories. Each tag is trimmed of whitespace. Cannot
    /// be null or empty.</param>
    /// <param name="baseFolder">The root folder to scan for Git repositories. Must be a valid directory path.</param>
    /// <param name="removeRemote">A boolean value indicating whether the tags should also be removed from remote repositories. <see
    /// langword="true"/> to remove tags from both local and remote repositories; otherwise, <see langword="false"/> to
    /// remove tags only locally.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the tag removal process is finished.</returns>
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

    /// <summary>
    /// Shows initial information about the base folder and tags to search for.
    /// </summary>
    /// <param name="baseFolder">
    /// The base folder where the Git repositories are located.
    /// </param>
    /// <param name="tagsToSearch">
    /// The tags to search for in the repositories.
    /// </param>
    private void ShowInitialInfo(string baseFolder, string[] tagsToSearch)
    {
        _console.MarkupLineInterpolated($"[blue]Base folder: [bold]{baseFolder}[/][/]");
        _console.MarkupLineInterpolated($"[blue]Tags to search: [bold]{string.Join(", ", tagsToSearch)}[/][/]");
    }

    /// <summary>
    /// Finds all repositories that contain the specified tags.
    /// </summary>
    /// <param name="allGitFolders">
    /// The list of all Git repository paths to search for tags.
    /// </param>
    /// <param name="tagsToSearch">
    /// The tags to search for in the repositories. Each tag is trimmed of whitespace.
    /// </param>
    /// <returns>
    /// A list of repositories that contain the specified tags, a dictionary mapping repository paths to lists of found tags,
    /// </returns>
    private async Task<(List<string> reposWithTag, Dictionary<string, List<string>> repoTagsMap, Dictionary<string, Exception> scanErrors)> FindRepositoriesWithTagsAsync
    (
        List<string> allGitFolders,
        string[] tagsToSearch
    )
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

                    await DetectTagsInRepositoryAsync(tagsToSearch, reposWithTag, repoTagsMap, scanErrors, repo).ConfigureAwait(false);
                }

                task.StopTask();
                task.Description("Scanning completed.");
            }).ConfigureAwait(false);

        return (reposWithTag, repoTagsMap, scanErrors);
    }

    /// <summary>
    /// Detects specified tags in a given repository asynchronously.
    /// </summary>
    /// <param name="tagsToSearch">
    /// The tags to search for in the repository. Each tag is trimmed of whitespace.
    /// </param>
    /// <param name="reposWithTag">
    /// The list to which the repository will be added if it contains any of the specified tags.
    /// </param>
    /// <param name="repoTagsMap">
    /// The dictionary mapping repository paths to lists of found tags.
    /// </param>
    /// <param name="scanErrors">
    /// The dictionary to store any exceptions that occur during the tag detection process, keyed by repository path.
    /// </param>
    /// <param name="repo">
    /// The path to the repository in which to search for the specified tags.
    /// </param>
    private async Task DetectTagsInRepositoryAsync
    (
        string[] tagsToSearch,
        List<string> reposWithTag,
        Dictionary<string, List<string>> repoTagsMap,
        Dictionary<string, Exception> scanErrors,
        string repo
    )
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

    /// <summary>
    /// Shows scan errors if any were detected during the tag removal process.
    /// </summary>
    /// <param name="scanErrors">
    /// The dictionary containing scan errors, where the key is the repository path and the value is the exception that occurred.
    /// </param>
    /// <param name="baseFolder">
    /// The base folder used to determine the hierarchical name of each repository.
    /// </param>
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

    /// <summary>
    /// Prompts the user to select repositories from a list of repositories that contain the specified tags.
    /// </summary>
    /// <param name="repoTagsMap">
    /// The dictionary mapping repository paths to lists of tags found in each repository.
    /// </param>
    /// <param name="reposWithTag">
    /// The list of repository paths that contain at least one of the specified tags.
    /// </param>
    /// <param name="baseFolder">
    /// The base folder used to determine the hierarchical name of each repository.
    /// </param>
    /// <param name="tagsToSearch">
    /// The tags to search for in the repositories. Each tag is trimmed of whitespace.
    /// </param>
    /// <returns>
    /// The list of selected repository paths, where each path corresponds to a repository that the user has chosen to remove the specified tags from.
    /// </returns>
    private List<string> PromptRepositorySelection(Dictionary<string, List<string>> repoTagsMap, List<string> reposWithTag, string baseFolder, string[] tagsToSearch)
    {
        var displayNameToPath = reposWithTag.ToDictionary
        (
            repo => $"{GetHierarchicalName(repo, baseFolder)} ({string.Join(", ", repoTagsMap[repo])})",
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
    /// <remarks>This method iterates through the provided repositories and attempts to remove the specified
    /// tags. If all tags are successfully removed from a repository, a success message is displayed in the
    /// console.</remarks>
    /// <param name="selectedPaths">A list of repository paths from which tags should be removed.</param>
    /// <param name="repoTagsMap">A dictionary mapping repository paths to the list of tags to be removed from each repository.</param>
    /// <param name="baseFolder">The base folder used to determine the hierarchical name of each repository.</param>
    /// <param name="removeRemote">A boolean value indicating whether the tags should also be removed from the remote repository. <see
    /// langword="true"/> to remove tags from the remote repository; otherwise, <see langword="false"/>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

            foreach (var tag in repoTagsMap[repo])
                success = await TryRemoveTagAsync(removeRemote, repo, hierarchicalName, success, tag).ConfigureAwait(false);

            if (success)
                _console.MarkupLineInterpolated($"[green]✅ {hierarchicalName}[/]");
        }
    }

    /// <summary>
    /// Tries to remove a tag from the specified repository.
    /// </summary>
    /// <param name="removeRemote">
    /// Determines whether to also remove the tag from the remote repository.
    /// </param>
    /// <param name="repo">
    /// The path to the repository from which the tag should be removed.
    /// </param>
    /// <param name="hierarchicalName">
    /// The hierarchical name of the repository, used for display purposes.
    /// </param>
    /// <param name="success">
    /// Indicates whether the previous tag removal operations were successful.
    /// </param>
    /// <param name="tag">
    /// The name of the tag to be removed.
    /// </param>
    /// <returns>
    /// True if the tag was successfully removed; otherwise, false.
    /// </returns>
    private async Task<bool> TryRemoveTagAsync(bool removeRemote, string repo, string hierarchicalName, bool success, string tag)
    {
        try
        {
            await _tagService.DeleteTagAsync(repo, tag).ConfigureAwait(false);

            if (removeRemote)
                await _tagService.DeleteRemoteTagAsync(repo, tag).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _console.MarkupLineInterpolated($"[red]❌ {hierarchicalName} (tag: {tag}): {ex.Message}[/]");
            success = false;
        }

        return success;
    }

    /// <summary>
    /// Shows the final status of the tag removal operation, including any errors that occurred during the scan.
    /// </summary>
    /// <param name="scanErrors">
    /// The dictionary containing scan errors, where the key is the repository path and the value is the exception that occurred.
    /// </param>
    /// <param name="baseFolder">
    /// The base folder used to determine the hierarchical name of each repository.
    /// </param>
    private void ShowFinalStatus(Dictionary<string, Exception> scanErrors, string baseFolder)
    {
        _console.MarkupLine("[bold green]✅ Done![/]");
        ShowScanErrors(scanErrors, baseFolder);
    }

    /// <summary>
    /// Gets the hierarchical name of a repository based on its path relative to the base folder.
    /// </summary>
    /// <param name="repoPath">
    /// The path to the repository for which to get the hierarchical name.
    /// </param>
    /// <param name="baseFolder">
    /// The base folder used to calculate the relative path of the repository.
    /// </param>
    /// <returns>
    /// The hierarchical name of the repository, which is its relative path from the base folder, with directory separators replaced by slashes.
    /// </returns>
    private static string GetHierarchicalName(string repoPath, string baseFolder)
    {
        var relativePath = Path.GetRelativePath(baseFolder, repoPath).Replace(Path.DirectorySeparatorChar, '/');

        return relativePath.Length <= 1 // If the path is just the root or empty, return the repo name
            ? Path.GetFileName(repoPath)
            : relativePath;
    }
}
