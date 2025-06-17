using System.CommandLine;
using System.IO.Abstractions;
using System.Text.Json;
using GitTools.Models;
using GitTools.Services;
using Spectre.Console;

namespace GitTools.Commands;

/// <summary>
/// Command for generating a JSON file with repository information.
/// </summary>
public sealed class BulkBackupCommand : Command
{
    private readonly IGitRepositoryScanner _gitScanner;
    private readonly IGitService _gitService;
    private readonly IFileSystem _fileSystem;
    private readonly IAnsiConsole _console;

    private const string DEFAULT_OUTPUT = "repositories.json";

    public BulkBackupCommand(
        IGitRepositoryScanner gitScanner,
        IGitService gitService,
        IFileSystem fileSystem,
        IAnsiConsole console)
        : base("bkp", "Generates a JSON file with remote URLs for each repository found.")
    {
        _gitScanner = gitScanner;
        _gitService = gitService;
        _fileSystem = fileSystem;
        _console = console;

        var directoryArgument = new Argument<string>("directory", "Root directory of git repositories");
        var outputArgument = new Argument<string?>("output", () => DEFAULT_OUTPUT, "Path to output JSON file");

        AddArgument(directoryArgument);
        AddArgument(outputArgument);

        this.SetHandler(ExecuteAsync, directoryArgument, outputArgument);
    }

    public async Task ExecuteAsync(string directory, string? output)
    {
        output ??= DEFAULT_OUTPUT;

        var repoPaths = _console.Status()
            .Start(
                $"[yellow]Scanning for Git repositories in {directory}...[/]",
                _ => _gitScanner.Scan(directory)
            );

        if (repoPaths.Count == 0)
        {
            _console.MarkupLine("[yellow]No Git repositories found.[/]");
        }

        var repositories = new List<GitRepository>();

        foreach (var repoPath in repoPaths)
        {
            if (IsSubmodule(repoPath))
                continue;

            var repo = await _gitService.GetGitRepositoryAsync(repoPath).ConfigureAwait(false);

            if (!repo.IsValid)
                continue;

            repositories.Add(new GitRepository
            {
                Name = _fileSystem.Path.GetFileName(repoPath),
                Path = repoPath,
                RemoteUrl = repo.RemoteUrl
            });
        }

        var json = JsonSerializer.Serialize(repositories, new JsonSerializerOptions { WriteIndented = true });
        _fileSystem.File.WriteAllText(output, json);

        _console.MarkupLineInterpolated($"[green]{repositories.Count} repositories processed.[/]");
        _console.MarkupLineInterpolated($"[blue]Configuration written to {output}[/]");
    }

    private bool IsSubmodule(string repoPath)
    {
        var gitFile = _fileSystem.Path.Combine(repoPath, ".git");

        if (!_fileSystem.File.Exists(gitFile))
            return false;

#pragma warning disable S6966
        var content = _fileSystem.File.ReadAllText(gitFile);
#pragma warning restore S6966

        return content.Contains("/modules/", StringComparison.OrdinalIgnoreCase);
    }
}

