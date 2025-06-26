using System.CommandLine;
using GitTools.Models;
using GitTools.Services;
using Spectre.Console;

namespace GitTools.Commands;

/// <summary>
/// Command for checking and synchronize outdated repositories.
/// </summary>
public sealed class SynchronizeCommand : Command
{
    private readonly IGitRepositoryScanner _scanner;
    private readonly IGitService _gitService;
    private readonly IAnsiConsole _console;
    private readonly IConsoleDisplayService _displayService;

    public SynchronizeCommand(IGitRepositoryScanner scanner, IGitService gitService, IAnsiConsole console, IConsoleDisplayService displayService)
        : base("sync", "Checks and synchronize repositories with the remote server.")
    {
        _scanner = scanner;
        _gitService = gitService;
        _console = console;
        _displayService = displayService;

        var rootArg = new Argument<string>("root-directory")
        {
            Description = "Root directory of git repositories",
            Arity = ArgumentArity.ExactlyOne
        };

        var showOnlyOption = new Option<bool>("--show-only", "-so")
        {
            Description = "Do not update repositories, just show which ones are outdated"
        };

        var withUncommittedOption = new Option<bool>("--with-uncommitted", "-wu")
        {
            Description = "Try to update repositories with uncommitted changes"
        };

        var pushUntrackedBranchesOption = new Option<bool>("--push-untracked", "-pu")
        {
            Description = "Push untracked branches to the remote repository"
        };

        var automaticOption = new Option<bool>("--automatic", "-a")
        {
            Description = "Run the command without user interaction (useful for scripts)"
        };

        var noFetchOption = new Option<bool>("--no-fetch", "-nf")
        {
            Description = "Do not fetch from remote before checking repositories"
        };

        Arguments.Add(rootArg);
        Options.Add(showOnlyOption);
        Options.Add(withUncommittedOption);
        Options.Add(pushUntrackedBranchesOption);
        Options.Add(automaticOption);
        Options.Add(noFetchOption);

        SetAction
        (
            (parseResult) => ExecuteAsync
            (
                parseResult.GetValue(rootArg)!,
                parseResult.GetValue(showOnlyOption),
                parseResult.GetValue(withUncommittedOption),
                parseResult.GetValue(pushUntrackedBranchesOption),
                parseResult.GetValue(automaticOption),
                parseResult.GetValue(noFetchOption)
            )
        );
    }

    private async Task ExecuteAsync(string rootDirectory, bool showOnly, bool withUncommited, bool pushUntrackedBranches, bool automatic, bool noFetch)
    {
        var repoPaths = _console.Status()
            .Start
            (
                $"[yellow]Scanning for Git repositories in {rootDirectory}...[/]",
                _ => _scanner.Scan(rootDirectory)
            );

        if (repoPaths.Count == 0)
        {
            _console.MarkupLine("[yellow]No Git repositories found.[/]");

            return;
        }

        var reposStatus = await _console.Progress()
            .Columns(
                new ProgressBarColumn
                {
                    CompletedStyle = new Style(foreground: Color.Green1, decoration: Decoration.Conceal | Decoration.Bold | Decoration.Invert),
                    RemainingStyle = new Style(decoration: Decoration.Conceal),
                    FinishedStyle = new Style(foreground: Color.Green1, decoration: Decoration.Conceal | Decoration.Bold | Decoration.Invert)
                },
                new PercentageColumn(),
                new SpinnerColumn(),
                new TaskDescriptionColumn())
            .StartAsync(ctx => GetRepositoriesStatusAsync(ctx, repoPaths, rootDirectory, !noFetch))
            .ConfigureAwait(false);

        var outdatedRepos = reposStatus
            .Where(static r => !r.AreBranchesSynced)
            .ToList();

        if (outdatedRepos.Count == 0)
        {
            _console.MarkupLine("[green]No out-of-sync repositories found.[/]");

            return;
        }

        _displayService.DisplayRepositoriesStatus(outdatedRepos, rootDirectory);

        if (showOnly)
            return;

        var selected = automatic
            ? outdatedRepos.ConvertAll(r => r.RepoPath)
            : await _console.PromptAsync
            (
                new MultiSelectionPrompt<string>()
                    .Title("[green]Select repositories to update[/]")
                    .NotRequired()
                    .PageSize(20)
                    .MoreChoicesText("[grey](Use space to select, enter to confirm)[/]")
                    .InstructionsText("[grey](Press [blue]<space>[/] to select, [green]<enter>[/] to confirm)[/]")
                    .AddChoiceGroup("Select all", outdatedRepos.Select(static r => r.RepoPath))
                    .UseConverter(r => r is "Select all" ? "Select all" : _displayService.GetHierarchicalName(r, rootDirectory))
            ).ConfigureAwait(false);

        var toUpdate = outdatedRepos
            .Where(r => selected.Contains(r.RepoPath))
            .ToList();

        if (toUpdate.Count == 0)
        {
            _console.MarkupLine("[yellow]No repository selected.[/]");

            return;
        }

        var (success, fail) = await _console.Progress()
            .Columns(
                new ProgressBarColumn
                {
                    CompletedStyle = new Style(foreground: Color.Green1, decoration: Decoration.Conceal | Decoration.Bold | Decoration.Invert),
                    RemainingStyle = new Style(decoration: Decoration.Conceal),
                    FinishedStyle = new Style(foreground: Color.Green1, decoration: Decoration.Conceal | Decoration.Bold | Decoration.Invert)
                },
                new PercentageColumn(),
                new SpinnerColumn(),
                new TaskDescriptionColumn())
            .StartAsync(ctx => UpdateRepositoriesAsync(withUncommited, ctx, toUpdate, pushUntrackedBranches))
            .ConfigureAwait(false);

        _console.MarkupLineInterpolated($"[blue]{success} succeeded, {fail} failed.[/]");
    }

    /// <summary>
    /// Asynchronously updates a list of Git repositories, optionally stashing uncommitted changes.
    /// </summary>
    /// <remarks>This method iterates over each repository in the <paramref name="toUpdate"/> list, updating
    /// each one. If a repository has no local branches, it is skipped. If <paramref name="withUncommited"/> is  <see
    /// langword="true"/> and the repository has uncommitted changes, those changes are stashed before  updating. The
    /// method logs progress and results to the console.</remarks>
    /// <param name="withUncommited">A boolean value indicating whether to stash uncommitted changes before updating.  If <see langword="true"/>,
    /// uncommitted changes will be stashed.</param>
    /// <param name="ctx">The <see cref="ProgressContext"/> used to track the progress of the update operation.</param>
    /// <param name="toUpdate">A list of <see cref="GitRepositoryStatus"/> objects representing the repositories to be updated.</param>
    /// <param name="pushUntrackedBranches">
    /// true to push untracked branches to the remote repository; otherwise, false.
    /// </param>
    /// <returns>A tuple containing the number of successful updates and the number of failed updates.</returns>
    private async Task<(int success, int fail)> UpdateRepositoriesAsync
    (
        bool withUncommited,
        ProgressContext ctx,
        List<GitRepositoryStatus> toUpdate,
        bool pushUntrackedBranches
    )
    {
        var success = 0;
        var task = ctx.AddTask("Updating repositories...");
        task.MaxValue = toUpdate.Count;

        foreach (var repo in toUpdate)
        {
            task.Description($"Updating {repo.HierarchicalName}...");

            var ok = await _gitService
                .SynchronizeRepositoryAsync
                (
                    repo,
                    msg => _console.MarkupLineInterpolated(msg),
                    withUncommited,
                    pushUntrackedBranches
                )
                .ConfigureAwait(false);

            if (ok)
                success++;

            task.Increment(1);
        }

        task.StopTask();

        return (success, toUpdate.Count - success);
    }

    /// <summary>
    /// Asynchronously retrieves the status of multiple Git repositories.
    /// </summary>
    /// <param name="ctx">The progress context used to report the status of the operation.</param>
    /// <param name="repoPaths">A list of file paths to the repositories to be checked.</param>
    /// <param name="rootDirectory">The root directory.</param>
    /// <param name="fetch">
    /// true to fetch the latest changes from the remote repository before checking the status; otherwise,
    /// </param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of <see
    /// cref="GitRepositoryStatus"/> objects representing the status of each repository that is not synced or has
    /// uncommitted changes if specified.</returns>
    private async Task<List<GitRepositoryStatus>> GetRepositoriesStatusAsync
    (
        ProgressContext ctx,
        List<string> repoPaths,
        string rootDirectory,
        bool fetch
    )
    {
        var statuses = new List<GitRepositoryStatus>();
        var task = ctx.AddTask("Checking repositories...");
        task.MaxValue = repoPaths.Count;

        foreach (var repo in repoPaths)
        {
            task.Description($"Checking {_displayService.GetHierarchicalName(repo, rootDirectory)}...");

            try
            {
                var repoStatus = await _gitService.GetRepositoryStatusAsync(repo, rootDirectory, fetch).ConfigureAwait(false);

                statuses.Add(repoStatus);
            }
            catch (Exception ex)
            {
                _console.MarkupLineInterpolated($"[red]✗[/] [grey]{repo} failed: {ex.Message}[/]");
            }

            task.Increment(1);
        }

        task.StopTask();

        return statuses;
    }
}
