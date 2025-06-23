using System.CommandLine;
using System.IO.Abstractions;
using System.Text.Json;
using GitTools.Models;
using GitTools.Services;
using Spectre.Console;

namespace GitTools.Commands;

/// <summary>
/// Command for restoring repositories from a backup configuration file.
/// </summary>
public sealed class BulkRestoreCommand : Command
{
    private readonly IGitService _gitService;
    private readonly IFileSystem _fileSystem;
    private readonly IAnsiConsole _console;

    public BulkRestoreCommand(
        IGitService gitService,
        IFileSystem fileSystem,
        IAnsiConsole console)
        : base("restore", "Clones repositories from a backup configuration file.")
    {
        _gitService = gitService;
        _fileSystem = fileSystem;
        _console = console;

        var configArg = new Argument<string>("config-file", "Path to JSON produced by backup");
        var directoryArg = new Argument<string>("directory", "Target root directory");
        var forceSshOption = new Option<bool>("--force-ssh", "Force SSH URLs for cloning repositories");

        AddArgument(configArg);
        AddArgument(directoryArg);
        AddOption(forceSshOption);

        this.SetHandler(ExecuteAsync, configArg, directoryArg, forceSshOption);
    }

    /// <summary>
    /// Executes the restoration process.
    /// </summary>
    public async Task ExecuteAsync(string configFile, string directory, bool forceSsh)
    {
        if (!_fileSystem.File.Exists(configFile))
        {
            _console.MarkupLineInterpolated($"[red]Configuration file not found: {configFile}[/]");

            return;
        }

        List<GitRepository>? repositories;

        try
        {
            var json = await _fileSystem.File.ReadAllTextAsync(configFile).ConfigureAwait(false);

            repositories = JsonSerializer.Deserialize<List<GitRepository>>
            (
                json,
                GitRepository.JsonSerializerOptions
            )
            ?.Where(static r => !string.IsNullOrWhiteSpace(r.RemoteUrl))
            .OrderBy(static r => r.Name).ToList();
        }
        catch (Exception ex)
        {
            _console.MarkupLineInterpolated($"[red]Failed to read configuration: {ex.Message}[/]");

            return;
        }

        if (repositories is null || repositories.Count == 0)
        {
            _console.MarkupLine("[yellow]No repositories found in the configuration file.[/]");

            return;
        }

        var selected = await _console.PromptAsync
        (
            new MultiSelectionPrompt<string>()
                .Title("[green]Select the repositories to restore[/]")
                .NotRequired()
                .PageSize(20)
                .MoreChoicesText("[grey](Use space to select, enter to confirm)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to select, [green]<enter>[/] to confirm)[/]")
                .AddChoiceGroup("Select all", repositories.Select(static r => r.Path))
        ).ConfigureAwait(false);

        if (selected.Count != repositories.Count)
            repositories = [.. repositories.Where(repo => selected.Contains(repo.Path))];

        if (repositories.Count == 0)
        {
            _console.MarkupLine("[yellow]No repositories selected for restore.[/]");

            return;
        }

        _fileSystem.Directory.CreateDirectory(directory);

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
            .StartAsync(ctx => RestoreRepositoriesAsync(directory, ctx, repositories, forceSsh))
            .ConfigureAwait(false);
    }

    private async Task RestoreRepositoriesAsync(string directory, ProgressContext ctx, List<GitRepository> repositories, bool forceSsh)
    {
        var task = ctx.AddTask("Cloning repositories...");
        task.MaxValue = repositories.Count;

        foreach (var repo in repositories)
        {
            task.Description($"Cloning {repo.Name}...");

            try
            {
                var url = forceSsh ? ConvertToSsh(repo.RemoteUrl!) : repo.RemoteUrl;
                await _gitService.RunGitCommandAsync(directory, $"clone {url} {repo.Name}").ConfigureAwait(false);
                _console.MarkupLineInterpolated($"[green]✓[/] [grey]{repo.Name} cloned successfully.[/]");
            }
            catch (Exception ex)
            {
                _console.MarkupLineInterpolated($"[red]✗[/] [grey]{repo.Name} failed: {ex.Message}[/]");
            }

            task.Increment(1);
        }

        task.StopTask();
        task.Description("Clone completed.");
    }

    private static string ConvertToSsh(string url)
    {
        if (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase) || url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
            return url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        var host = uri.Host;
        var path = uri.AbsolutePath.TrimStart('/');

        return $"git@{host}:{path}";
    }
}
