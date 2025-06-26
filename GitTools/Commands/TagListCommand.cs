using System.CommandLine;
using GitTools.Services;
using Spectre.Console;

namespace GitTools.Commands;

/// <summary>
/// Command for listing repositories that contain specified tags.
/// </summary>
public sealed class TagListCommand : Command
{
    private readonly ITagSearchService _tagSearchService;
    private readonly ITagValidationService _tagValidationService;
    private readonly IConsoleDisplayService _consoleDisplayService;
    private readonly IAnsiConsole _console;

    public TagListCommand
    (
        ITagSearchService tagSearchService,
        ITagValidationService tagValidationService,
        IConsoleDisplayService consoleDisplayService,
        IAnsiConsole console
    )
        : base("ls", "Lists repositories containing specified tags.")
    {
        _tagSearchService = tagSearchService;
        _tagValidationService = tagValidationService;
        _consoleDisplayService = consoleDisplayService;
        _console = console;

        var dirArgument = new Argument<string>("directory")
        {
            Description = "Root directory of git repositories",
            Arity = ArgumentArity.ExactlyOne
        };

        var tagsArgument = new Argument<string>("tags")
        {
            Description = "Tags to search (comma separated)",
            Arity = ArgumentArity.ExactlyOne
        };

        Arguments.Add(dirArgument);
        Arguments.Add(tagsArgument);

        SetAction
        (
            parseResult => ExecuteAsync
            (
                parseResult.GetValue(tagsArgument)!,
                parseResult.GetValue(dirArgument)!
            )
        );
    }

    /// <summary>
    /// Executes the tag listing operation.
    /// </summary>
    private async Task ExecuteAsync(string tagsInput, string baseFolder)
    {
        var patterns = _tagValidationService.ParseAndValidateTags(tagsInput);

        if (patterns.Length == 0)
        {
            _console.MarkupLine("[red]No tags specified to search.[/]");

            return;
        }

        var searchResult = await _console.Status().StartAsync
        (
            $"[yellow]Searching {patterns.Length} tags...[/]",
            ctx => _tagSearchService.SearchRepositoriesWithTagsAsync
            (
                baseFolder,
                patterns,
                repoName => ctx.Status = $"[yellow]Scanning repository {repoName}...[/]"
            )
        ).ConfigureAwait(false);

        if (searchResult.RepositoriesWithTags.Count == 0)
        {
            _console.MarkupLine("[yellow]No repository with the specified tag(s) found.[/]");
            _consoleDisplayService.ShowScanErrors(searchResult.ScanErrors, baseFolder);

            return;
        }

        foreach (var (repo, tagsFound) in searchResult.RepositoryTagsMap)
        {
            var hierarchicalName = _consoleDisplayService.GetHierarchicalName(repo, baseFolder);
            _console.MarkupLineInterpolated($"[blue]{hierarchicalName}[/]: {string.Join(", ", tagsFound)}");
        }

        _consoleDisplayService.ShowScanErrors(searchResult.ScanErrors, baseFolder);
    }
}
