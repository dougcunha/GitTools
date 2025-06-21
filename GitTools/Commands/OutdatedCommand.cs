using System.CommandLine;
using GitTools.Services;
using Spectre.Console;

namespace GitTools.Commands;

/// <summary>
/// Command for checking and updating outdated repositories.
/// </summary>
public sealed class OutdatedCommand : Command
{
    private readonly IGitRepositoryScanner _scanner;
    private readonly IGitService _gitService;
    private readonly IAnsiConsole _console;

    public OutdatedCommand(IGitRepositoryScanner scanner, IGitService gitService, IAnsiConsole console)
        : base("outdated", "Checks for outdated repositories and optionally updates them.")
    {
        _scanner = scanner;
        _gitService = gitService;
        _console = console;

        var rootArg = new Argument<string>("root-directory", "Root directory of git repositories");
        var branchOption = new Option<string>("--branch", () => "main", "Branch to compare against");
        var updateOption = new Option<bool>("--update", "Automatically update all outdated repositories");
        var withUncommitedOption = new Option<bool>("--with-uncommited", "Include repositories with uncommitted changes");

        AddArgument(rootArg);
        AddOption(branchOption);
        AddOption(updateOption);
        AddOption(withUncommitedOption);

        this.SetHandler(ExecuteAsync, rootArg, branchOption, updateOption, withUncommitedOption);
    }

    public async Task ExecuteAsync(string rootDirectory, string branch, bool update, bool withUncommited)
    {
        var repoPaths = _console.Status()
            .Start($"[yellow]Scanning for Git repositories in {rootDirectory}...[/]", _ => _scanner.Scan(rootDirectory));

        if (repoPaths.Count == 0)
        {
            _console.MarkupLine("[yellow]No Git repositories found.[/]");
            return;
        }

        var outdated = new List<string>();

        await _console.Progress()
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
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Checking repositories...");
                task.MaxValue = repoPaths.Count;

                foreach (var repo in repoPaths)
                {
                    task.Description($"Checking {Path.GetFileName(repo)}...");
                    try
                    {
                        var status = await _gitService.RunGitCommandAsync(repo, "status --porcelain").ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(status) && !withUncommited)
                        {
                            _console.MarkupLineInterpolated($"[yellow]⚠[/] [grey]{repo} has uncommitted changes. Skipping.[/]");
                            task.Increment(1);
                            continue;
                        }

                        await _gitService.RunGitCommandAsync(repo, "fetch origin").ConfigureAwait(false);
                        var countStr = await _gitService.RunGitCommandAsync(repo, $"rev-list --count HEAD..origin/{branch}").ConfigureAwait(false);
                        if (int.TryParse(countStr.Trim(), out var count) && count > 0)
                            outdated.Add(repo);
                    }
                    catch (Exception ex)
                    {
                        _console.MarkupLineInterpolated($"[red]✗[/] [grey]{repo} failed: {ex.Message}[/]");
                    }

                    task.Increment(1);
                }

                task.StopTask();
            }).ConfigureAwait(false);

        if (outdated.Count == 0)
        {
            _console.MarkupLine("[green]All repositories are up to date.[/]");
            return;
        }

        List<string> toUpdate = outdated;

        if (!update)
        {
            var selected = await _console.PromptAsync(
                new MultiSelectionPrompt<string>()
                    .Title("[green]Select repositories to update[/]")
                    .NotRequired()
                    .PageSize(20)
                    .MoreChoicesText("[grey](Use space to select, enter to confirm)[/]")
                    .InstructionsText("[grey](Press [blue]<space>[/] to select, [green]<enter>[/] to confirm)[/]")
                    .AddChoiceGroup("Select all", outdated)).ConfigureAwait(false);

            if (selected.Count == 0)
            {
                _console.MarkupLine("[yellow]No repository selected.[/]");
                return;
            }

            toUpdate = selected;
        }

        var success = 0;
        var fail = 0;

        await _console.Progress()
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
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Updating repositories...");
                task.MaxValue = toUpdate.Count;

                foreach (var repo in toUpdate)
                {
                    task.Description($"Updating {Path.GetFileName(repo)}...");
                    try
                    {
                        if (withUncommited)
                        {
                            var status = await _gitService.RunGitCommandAsync(repo, "status --porcelain").ConfigureAwait(false);
                            if (!string.IsNullOrWhiteSpace(status))
                                await _gitService.RunGitCommandAsync(repo, "stash --include-untracked").ConfigureAwait(false);
                        }

                        await _gitService.RunGitCommandAsync(repo, $"pull origin {branch}").ConfigureAwait(false);
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
            }).ConfigureAwait(false);

        _console.MarkupLineInterpolated($"[blue]{success} succeeded, {fail} failed.[/]");
    }
}

