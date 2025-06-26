using System.CommandLine;
using System.IO.Abstractions;
using System.Text.Json;
using GitTools.Json;
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

    public BulkBackupCommand
    (
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

        var directoryArgument = new Argument<string>("directory")
        {
            Description = "Root directory of git repositories",
            Arity = ArgumentArity.ExactlyOne
        };

        var outputArgument = new Argument<string>("output")
        {
            Description = "Path to output JSON file",
            Arity = ArgumentArity.ExactlyOne
        };

        Arguments.Add(directoryArgument);
        Arguments.Add(outputArgument);

        SetAction
        (
            parseResult => ExecuteAsync
            (
                parseResult.GetValue(directoryArgument)!,
                parseResult.GetValue(outputArgument)!
            )
        );
    }

    private async Task ExecuteAsync(string directory, string output)
    {
        var repoPaths = _console.Status()
            .Start
            (
                $"[yellow]Scanning for Git repositories in {directory}...[/]",
                _ => _gitScanner.Scan(directory)
            );

        if (repoPaths.Count == 0)
        {
            _console.MarkupLine("[yellow]No Git repositories found.[/]");

            return;
        }

        var repositories = new List<GitRepository>();

        await _console.Status()
            .StartAsync
            (
                $"[yellow]Processing {repoPaths.Count} repositories...[/]",
                _ => CollectRepositoriesAsync(repoPaths, repositories)
            ).ConfigureAwait(false);

        var path = Path.GetFullPath(directory);

        var backupData = repositories.ConvertAll(r => new GitRepositoryBackup
        {
            Name = Path.GetFileName(r.Path),
            Path = Path.GetRelativePath(path, r.Path),
            RemoteUrl = r.RemoteUrl
        });

        var json = JsonSerializer.Serialize(backupData, GitToolsJsonContext.Default.ListGitRepositoryBackup);

        var outputDirectory = Path.GetDirectoryName(output);

        if (!string.IsNullOrEmpty(outputDirectory) && !_fileSystem.Directory.Exists(outputDirectory))
        {
            _fileSystem.Directory.CreateDirectory(outputDirectory);
        }

        await _fileSystem.File.WriteAllTextAsync(output, json).ConfigureAwait(false);

        _console.MarkupLineInterpolated($"[green]{repositories.Count} repositories processed.[/]");
        _console.MarkupLineInterpolated($"[blue]Configuration written to {output}[/]");
    }

    private async Task CollectRepositoriesAsync(List<string> repoPaths, List<GitRepository> repositories)
    {
        foreach (var repoPath in repoPaths)
        {
            if (await IsSubmoduleAsync(repoPath).ConfigureAwait(false))
                continue;

            var repo = await _gitService.GetGitRepositoryAsync(repoPath).ConfigureAwait(false);

            if (!repo.IsValid)
                continue;

            repositories.Add(repo);
        }
    }

    private async Task<bool> IsSubmoduleAsync(string repoPath)
    {
        var gitFile = Path.Combine(repoPath, ".git");

        if (!_fileSystem.File.Exists(gitFile))
            return false;

        var content = await _fileSystem.File.ReadAllTextAsync(gitFile).ConfigureAwait(false);

        return content.Contains("/modules/", StringComparison.OrdinalIgnoreCase);
    }
}
