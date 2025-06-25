using GitTools.Models;
using Spectre.Console;

namespace GitTools.Services;

/// <summary>
/// Provides console display operations for git repository operations.
/// </summary>
public sealed class ConsoleDisplayService(IAnsiConsole console) : IConsoleDisplayService
{
    /// <inheritdoc />
    public string GetHierarchicalName(string repositoryPath, string baseFolder)
        => GitService.GetHierarchicalName(repositoryPath, baseFolder);

    /// <inheritdoc />
    public void ShowScanErrors(Dictionary<string, Exception> scanErrors, string baseFolder)
    {
        if (scanErrors.Count == 0 || !console.Confirm($"[red]{scanErrors.Count} scan errors detected. Do you want to see the details?[/]"))
            return;

        foreach (var (repoPath, exception) in scanErrors)
        {
            var rule = new Rule($"[red]{GetHierarchicalName(repoPath, baseFolder)}[/]")
                .RuleStyle(new Style(Color.Red))
                .Centered();

            console.Write(rule);

            // Use simple exception display for AOT compatibility
            console.MarkupLineInterpolated($"[red]Error: {exception.Message}[/]\n{exception.StackTrace}");
        }
    }

    /// <inheritdoc />
    public void ShowInitialInfo(string baseFolder, string[] tags)
    {
        console.MarkupLineInterpolated($"[blue]Base folder: [bold]{baseFolder}[/][/]");
        console.MarkupLineInterpolated($"[blue]Tags to search: [bold]{string.Join(", ", tags)}[/][/]");
    }

    /// <inheritdoc />
    public void DisplayRepositoriesStatus(List<GitRepositoryStatus> reposStatus, string baseFolder)
    {
        if (reposStatus.Count == 0)
        {
            console.MarkupLine("[green]No repositories found.[/]");

            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[yellow]Outdated Repositories[/]")
            .AddColumn("[grey]Repository[/]")
            .AddColumn("[blue]Remote URL[/]")
            .AddColumn("[red]A[/]")
            .AddColumn("[yellow]B[/]")
            .AddColumn("[darkgreen]T[/]")
            .AddColumn("[gold3]U[/]")
            .AddColumn("[red]E[/]");

        foreach (var repo in reposStatus)
        {
            var commitsAhead = repo.LocalBranches.Sum(static b => b.RemoteAheadCount);
            var commitsBehind = repo.LocalBranches.Sum(static b => b.RemoteBehindCount);

            var errorIndicator = repo.HasErrors
                ? "[red]x[/]"
                : string.Empty;

            table.AddRow
            (
                $"[grey]{GetHierarchicalName(repo.RepoPath, baseFolder)}[/]",
                $"[blue]{repo.RemoteUrl}[/]",
                $"[red]{commitsAhead}[/]",
                $"[yellow]{commitsBehind}[/]",
                $"[darkgreen]{repo.TrackedBranchesCount}[/]",
                $"[gold3]{repo.UntrackedBranchesCount}[/]",
                $"[red]{errorIndicator}[/]"
            );
        }

        console.Write(table);

        // Write a legend for the table
        console.MarkupLine
        (
            "[grey]A[/]: [red]Commits ahead of remote[/], " +
            "[yellow]B[/]: [yellow]Commits behind remote[/], " +
            "[darkgreen]T[/]: [darkgreen]Untracked branches[/], " +
            "[gold3]U[/]: [gold3]Tracked branches[/] , " +
            "[red]E[/]: [red]Error indicator[/]"
        );
    }
}
