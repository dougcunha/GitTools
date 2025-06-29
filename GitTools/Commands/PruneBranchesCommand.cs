using System.CommandLine;
using GitTools.Models;
using GitTools.Services;
using Spectre.Console;

namespace GitTools.Commands;

/// <summary>
/// Command for pruning local branches across multiple repositories.
/// </summary>
public sealed class PruneBranchesCommand : Command
{
    private readonly IGitRepositoryScanner _scanner;
    private readonly IGitService _gitService;
    private readonly IAnsiConsole _console;
    private readonly IConsoleDisplayService _displayService;

    public PruneBranchesCommand(IGitRepositoryScanner scanner, IGitService gitService, IAnsiConsole console, IConsoleDisplayService displayService)
        : base("prune-branches", "Prunes local branches in git repositories.")
    {
        _scanner = scanner;
        _gitService = gitService;
        _console = console;
        _displayService = displayService;

        Aliases.Add("pb");

        var rootArg = new Argument<string>("root-directory")
        {
            Description = "Root directory of git repositories",
            Arity = ArgumentArity.ExactlyOne
        };

        var mergedOption = new Option<bool>("--merged")
        {
            Description = "Include branches already merged into HEAD"
        };

        var goneOption = new Option<bool>("--gone")
        {
            Description = "Include branches whose upstream no longer exists"
        };

        var olderThanOption = new Option<int?>("--older-than")
        {
            Description = "Include branches with last commit older than specified days",
            Arity = ArgumentArity.ZeroOrOne
        };

        var automaticOption = new Option<bool>("--automatic", "-a")
        {
            Description = "Run without prompting for confirmation"
        };

        var dryRunOption = new Option<bool>("--dry-run")
        {
            Description = "Show what would be done without deleting branches"
        };

        Arguments.Add(rootArg);
        Options.Add(mergedOption);
        Options.Add(goneOption);
        Options.Add(olderThanOption);
        Options.Add(automaticOption);
        Options.Add(dryRunOption);

        SetAction
        (
            parseResult => ExecuteAsync
            (
                parseResult.GetValue(rootArg)!,
                parseResult.GetValue(mergedOption),
                parseResult.GetValue(goneOption),
                parseResult.GetValue(olderThanOption),
                parseResult.GetValue(automaticOption),
                parseResult.GetValue(dryRunOption)
            )
        );
    }

    private async Task ExecuteAsync(string rootDirectory, bool merged, bool gone, int? olderThan, bool automatic, bool dryRun)
    {
        if (!merged && !gone && olderThan is null)
            merged = true;

        var repoPaths = _console.Status()
            .Start($"[yellow]Scanning for Git repositories in {rootDirectory}...[/]", _ => _scanner.Scan(rootDirectory));

        if (repoPaths.Count == 0)
        {
            _console.MarkupLine("[yellow]No Git repositories found.[/]");
            return;
        }

        var repoBranches = await _console.Progress()
            .Columns(new ProgressBarColumn
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
                var result = new Dictionary<string, List<BranchStatus>>();
                var task = ctx.AddTask("Analyzing repositories...");
                task.MaxValue = repoPaths.Count;

                foreach (var repo in repoPaths)
                {
                    task.Description($"Checking {_displayService.GetHierarchicalName(repo, rootDirectory)}...");
                    var branches = await _gitService.GetPrunableBranchesAsync(repo, merged, gone, olderThan).ConfigureAwait(false);

                    if (branches.Count > 0)
                        result[repo] = branches;

                    task.Increment(1);
                }

                task.StopTask();

                return result;
            }).ConfigureAwait(false);

        if (repoBranches.Count == 0)
        {
            _console.MarkupLine("[yellow]No branches to prune found.[/]");

            return;
        }

        var branchKeys = repoBranches.SelectMany(kv => kv.Value.Select(b => $"{kv.Key}|{b.Name}|{b.CanBeSafelyDeleted}")).ToList();

        var selected = automatic ? branchKeys : await _console.PromptAsync(
            new MultiSelectionPrompt<string>()
                .Title("[green]Select branches to delete[/]")
                .NotRequired()
                .PageSize(20)
                .MoreChoicesText("[grey](Use space to select, enter to confirm)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to select, [green]<enter>[/] to confirm)[/]")
                .AddChoiceGroup("Select all", branchKeys)
                .UseConverter(key =>
                {
                    if (key == "Select all")
                        return key;

                    var parts = key.Split('|', 3);
                    var repo = parts[0];
                    var branch = parts[1];
                    var canBeDeleted = parts.Length > 2 && bool.Parse(parts[2]);
                    var canBeDeletedText = canBeDeleted ? "[green](can be safely deleted)[/]" : "[red](not fully merged)[/]";

                    return $"{_displayService.GetHierarchicalName(repo, rootDirectory)} > {branch} {canBeDeletedText}";
                }));

        if (selected.Count == 0)
        {
            _console.MarkupLine("[yellow]No branch selected.[/]");

            return;
        }

        if (dryRun)
        {
            foreach (var key in selected)
            {
                var (repo, branch) = SplitKey(key);
                _console.MarkupLineInterpolated($"[blue]{_displayService.GetHierarchicalName(repo, rootDirectory)} -> {branch}[/]");
            }

            _console.MarkupLine("[green]Dry run completed.[/]");

            return;
        }

        await _console.Progress()
            .Columns(new ProgressBarColumn
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
                var task = ctx.AddTask("Deleting branches...");
                task.MaxValue = selected.Count;

                foreach (var key in selected)
                {
                    var (repo, branch) = SplitKey(key);
                    task.Description($"Deleting {_displayService.GetHierarchicalName(repo, rootDirectory)} -> {branch}...");

                    try
                    {
                        await _gitService.DeleteLocalBranchAsync(repo, branch, force: true).ConfigureAwait(false);
                        _console.MarkupLineInterpolated($"[green]✓ {_displayService.GetHierarchicalName(repo, rootDirectory)} -> {branch}[/]");
                    }
                    catch (Exception ex)
                    {
                        _console.MarkupLineInterpolated($"[red]✗ {_displayService.GetHierarchicalName(repo, rootDirectory)} -> {branch}: {ex.Message}[/]");
                    }

                    task.Increment(1);
                }

                task.StopTask();
            }).ConfigureAwait(false);

        _console.MarkupLine("[green]Branch pruning completed.[/]");
    }

    private static (string repo, string branch) SplitKey(string key)
    {
        var parts = key.Split('|');
        var repo = parts[0];
        var branch = parts[1];

        return (repo, branch);
    }
}
