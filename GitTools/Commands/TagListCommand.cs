using System.CommandLine;
using GitTools.Services;
using GitTools.Utils;
using Spectre.Console;

namespace GitTools.Commands;

/// <summary>
/// Command for listing repositories that contain specified tags.
/// </summary>
public sealed class TagListCommand : Command
{
    private readonly IGitRepositoryScanner _gitScanner;
    private readonly IGitService _gitService;
    private readonly IAnsiConsole _console;

    public TagListCommand(
        IGitRepositoryScanner gitScanner,
        IGitService gitService,
        IAnsiConsole console)
        : base("ls", "Lists repositories containing specified tags.")
    {
        _gitScanner = gitScanner;
        _gitService = gitService;
        _console = console;

        var dirArgument = new Argument<string>("directory", "Root directory of git repositories");

        var tagsArgument = new Argument<string>("tags", "Tags to search (comma separated)")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        AddArgument(dirArgument);
        AddArgument(tagsArgument);

        this.SetHandler(ExecuteAsync, tagsArgument, dirArgument);
    }

    /// <summary>
    /// Executes the tag listing operation.
    /// </summary>
    public async Task ExecuteAsync(string tags, string baseFolder)
    {
        var patterns = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (patterns.Length == 0)
        {
            _console.MarkupLine("[red]No tags specified to search.[/]");

            return;
        }

        var scanErrors = new Dictionary<string, Exception>();
        var repoTagsMap = new Dictionary<string, List<string>>();

        await _console.Status().StartAsync
        (
            $"[yellow]Searching {patterns.Length} tags...[/]",
            ctx => ScanRepositoriesForTagsAsync(baseFolder, ctx, patterns, scanErrors, repoTagsMap)
        ).ConfigureAwait(false);

        if (repoTagsMap.Count == 0)
        {
            _console.MarkupLine("[yellow]No repository with the specified tag(s) found.[/]");
            ShowScanErrors(scanErrors, baseFolder);

            return;
        }

        foreach (var (repo, tagsFound) in repoTagsMap)
        {
            var hierarchicalName = GetHierarchicalName(repo, baseFolder);
            _console.MarkupLineInterpolated($"[blue]{hierarchicalName}[/]: {string.Join(", ", tagsFound)}");
        }

        ShowScanErrors(scanErrors, baseFolder);
    }

    private async Task ScanRepositoriesForTagsAsync(string baseFolder, StatusContext ctx, string[] patterns, Dictionary<string, Exception> scanErrors, Dictionary<string, List<string>> repoTagsMap)
    {
        var allGitFolders = _gitScanner.Scan(baseFolder);

        if (allGitFolders.Count == 0)
        {
            _console.MarkupLine("[red]No Git repositories found.[/]");

            return;
        }

        foreach (var repo in allGitFolders)
        {
            ctx.Status = $"[yellow]Scanning repository {Path.GetDirectoryName(repo)}...[/]";

            try
            {
                var allTags = await _gitService.GetAllTagsAsync(repo).ConfigureAwait(false);
                var matches = new List<string>();

                foreach (var pattern in patterns)
                    matches.AddRange(WildcardMatcher.MatchItems(allTags, pattern));

                matches = [.. matches.Distinct()];

                if (matches.Count > 0)
                    repoTagsMap[repo] = matches;
            }
            catch (Exception ex)
            {
                scanErrors[repo] = ex;
            }
        }

        ctx.Status = $"[green]{allGitFolders.Count} Git repositories scanned.[/]";
    }

    private void ShowScanErrors(Dictionary<string, Exception> scanErrors, string baseFolder)
    {
        if (scanErrors.Count == 0)
            return;

        if (!_console.Confirm($"[red]{scanErrors.Count} scan errors detected. Do you want to see the details?[/]"))
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

    private static string GetHierarchicalName(string repoPath, string baseFolder)
    {
        var relativePath = Path.GetRelativePath(baseFolder, repoPath).Replace(Path.DirectorySeparatorChar, '/');

        return relativePath.Length <= 1
            ? Path.GetFileName(repoPath)
            : relativePath;
    }
}
