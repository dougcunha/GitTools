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

    public SynchronizeCommand(IGitRepositoryScanner scanner, IGitService gitService, IAnsiConsole console)
        : base("sync", "Checks and synchronize repositories with the remote server.")
    {
        _scanner = scanner;
        _gitService = gitService;
        _console = console;

        var rootArg = new Argument<string>("root-directory", "Root directory of git repositories");
        var showOnlyOption = new Option<bool>("--show-only", "Do not update repositories, just show which ones are outdated");
        var withUncommitedOption = new Option<bool>("--with-uncommited", "Try to update repositories with uncommitted changes");

        AddArgument(rootArg);
        AddOption(showOnlyOption);
        AddOption(withUncommitedOption);

        this.SetHandler(ExecuteAsync, rootArg, showOnlyOption, withUncommitedOption);
    }

    public async Task ExecuteAsync(string rootDirectory, bool showOnly, bool withUncommited)
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
            .StartAsync(ctx => GetRepositoriesStatusAsync(withUncommited, ctx, repoPaths))
            .ConfigureAwait(false);

        if (reposStatus.Count == 0)
        {
            _console.MarkupLine("[green]All repositories are up to date.[/]");

            return;
        }

        if (showOnly)
        {
            DisplayOutdatedRepositories(reposStatus);

            return;
        }

        var selected = await _console.PromptAsync(
            new MultiSelectionPrompt<string>()
                .Title("[green]Select repositories to update[/]")
                .NotRequired()
                .PageSize(20)
                .MoreChoicesText("[grey](Use space to select, enter to confirm)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to select, [green]<enter>[/] to confirm)[/]")
                .AddChoiceGroup("Select all", reposStatus.Select(static r => r.Name))).ConfigureAwait(false);

        var toUpdate = reposStatus
            .Where(r => selected.Contains(r.Name))
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
            .StartAsync(ctx => UpdateRepositoriesAsync(withUncommited, ctx, toUpdate))
            .ConfigureAwait(false);

        _console.MarkupLineInterpolated($"[blue]{success} succeeded, {fail} failed.[/]");
    }

    private void DisplayOutdatedRepositories(List<GitRepositoryStatus> reposStatus)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[yellow]Outdated Repositories[/]")
            .AddColumn("[grey]Repository[/]")
            .AddColumn("[blue]Remote URL[/]")
            .AddColumn("[red]Ahead[/]")
            .AddColumn("[yellow]Behind[/]");

        foreach (var repo in reposStatus)
        {
            var commitsAhead = repo.LocalBranches.Sum(static b => b.RemoteAheadCount);
            var commitsBehind = repo.LocalBranches.Sum(static b => b.RemoteBehindCount);

            table.AddRow
            (
                $"[grey]{repo.Name}[/]",
                $"[blue]{repo.RemoteUrl}[/]",
                $"[red]{commitsAhead}[/]",
                $"[yellow]{commitsBehind}[/]"
            );
        }

        _console.Write(table);
    }

    private async Task<(int success, int fail)> UpdateRepositoriesAsync
    (
        bool withUncommited,
        ProgressContext ctx,
        List<GitRepositoryStatus> toUpdate
    )
    {
        var (success, fail) = (0, 0);
        var task = ctx.AddTask("Updating repositories...");
        task.MaxValue = toUpdate.Count;

        foreach (var repo in toUpdate)
        {
            task.Description($"Updating {Path.GetFileName(repo.Name)}...");
            try
            {
                bool hasStash = false;

                if (withUncommited && repo.HasUncommitedChanges)
                {
                    _console.MarkupLineInterpolated($"[yellow]⚠[/] [grey]{repo} has uncommitted changes. Stashing them.[/]");
                    hasStash = await _gitService.StashAsync(repo.Name, includeUntracked: true).ConfigureAwait(false);
                }

                if (repo.LocalBranches.Count == 0)
                {
                    _console.MarkupLineInterpolated($"[red]✗[/] [grey]{repo} has no local branches. Skipping.[/]");
                    fail++;
                    task.Increment(1);

                    continue;
                }

                foreach (var branch in repo.LocalBranches)
                {
                    if (!await _gitService.SynchronizeBranchAsync(repo.Name, branch.Name).ConfigureAwait(false))
                    {
                        _console.MarkupLineInterpolated($"[red]✗[/] [grey]{repo} failed to synchronize branch {branch.Name}.[/]");
                        fail++;

                        continue;
                    }
                }

                if (hasStash)
                    await _gitService.RunGitCommandAsync(repo.Name, "stash pop").ConfigureAwait(false);

                _console.MarkupLineInterpolated($"[green]✓[/] [grey]{repo} updated.[/]");
                success++;
            }
            catch (Exception ex)
            {
                _console.MarkupLineInterpolated($"[red]✗[/] [grey]{repo} failed: {ex.Message}[/]");
                fail++;
            }

            task.Increment(1);
        }

        task.StopTask();

        return (success, fail);
    }

    private async Task<List<GitRepositoryStatus>> GetRepositoriesStatusAsync
    (
        bool withUncommited,
        ProgressContext ctx,
        List<string> repoPaths
    )
    {
        var statuses = new List<GitRepositoryStatus>();
        var task = ctx.AddTask("Checking repositories...");
        task.MaxValue = repoPaths.Count;

        foreach (var repo in repoPaths)
        {
            task.Description($"Checking {Path.GetFileName(repo)}...");
            try
            {
                var repoStatus = await _gitService.GetRepositoryStatusAsync(repo).ConfigureAwait(false);

                if (repoStatus.HasErros)
                {
                    _console.MarkupLineInterpolated($"[red]✗[/] [grey]{repo} has errors: {repoStatus.ErrorMessage}[/]");
                    task.Increment(1);

                    continue;
                }

                if (!repoStatus.AreBranchesSynced && (!repoStatus.HasUncommitedChanges || withUncommited))
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

