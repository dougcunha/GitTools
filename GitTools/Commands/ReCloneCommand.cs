using System.CommandLine;
using System.IO.Abstractions;
using GitTools.Services;
using Spectre.Console;

namespace GitTools.Commands;

/// <summary>
/// Command for recloning the current git repository.
/// </summary>
public sealed class ReCloneCommand : Command
{
    private readonly IFileSystem _fileSystem;
    private readonly IBackupService _backupService;
    private readonly IGitService _gitService;
    private readonly IAnsiConsole _console;

    private readonly IProcessRunner _processRunner;

    public ReCloneCommand
    (
        IFileSystem fileSystem,
        IBackupService backupService,
        IGitService gitService,
        IAnsiConsole console,
        IProcessRunner processRunner
    )
        : base("reclone", "Reclones the specified git repository.")
    {
        _fileSystem = fileSystem;
        _backupService = backupService;
        _gitService = gitService;
        _console = console;
        _processRunner = processRunner;

        var repositoryNameArgument = new Argument<string>("repository-name", "Git repository folder name relative to the current directory to reclone");
        var noBackupOption = new Option<bool>("--no-backup", "Do not create a backup zip of the folder");
        var forceOption = new Option<bool>("--force", "Ignore uncommitted changes to the repository");

        AddArgument(repositoryNameArgument);
        AddOption(noBackupOption);
        AddOption(forceOption);

        this.SetHandler(ExecuteAsync, repositoryNameArgument, noBackupOption, forceOption);
    }

    /// <summary>
    /// Executes the reclone operation.
    /// </summary>
    public async Task ExecuteAsync(string repositoryName, bool noBackup, bool force)
    {
        // Normalize and validate the repository path
        var repo = await _gitService.GetGitRepositoryAsync(repositoryName).ConfigureAwait(false);

        if (!repo.IsValid)
        {
            _console.MarkupLineInterpolated($"[red]{repositoryName} is not valid or does not exist: {repo.Path}[/]");

            return;
        }

        _console.MarkupLineInterpolated($"[grey]Recloning repository: {repo.Name} at {repo.Path}[/]");

        if (!force)
        {
            var status = await _gitService.RunGitCommandAsync(repo.Path, "status --porcelain").ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(status))
            {
                _console.MarkupLine("[red]Uncommitted changes detected. Use --force to ignore.[/]");

                return;
            }
        }

        if (!noBackup)
            GenerateRepositoryBackup(repo.Path, repo.ParentDir, repo.Name);

        var tempPath = RenameRepositoryDirectory(repo.Path);

        await _console.Status()
            .StartAsync("[yellow]Cloning repository...[/]", async ctx =>
            {
                return await _gitService.RunGitCommandAsync(repo.ParentDir, $"clone {repo.RemoteUrl} {repo.Name}").ConfigureAwait(false);
            }).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(tempPath))
        {
            var deleteOldRepositoryResult = await _gitService.DeleteLocalGitRepositoryAsync(tempPath)
                .ConfigureAwait(false);

            if (deleteOldRepositoryResult)
                _console.MarkupLineInterpolated($"[green]✓[/] [grey]Old repository deleted: {tempPath}[/]");
        }

        _console.MarkupLine("[green]✓ Repository recloned successfully.[/]");
    }
   
    private void GenerateRepositoryBackup(string repoPath, string parentDir, string repoName)
    {
        var backupFile = Path.Combine(parentDir, $"{repoName}-backup.zip");

        _console.Status()
            .Start("[yellow]Creating backup...[/]", ctx =>
            {
                _backupService.CreateBackup(repoPath, backupFile);
            });

        _console.MarkupLineInterpolated($"[green]✓[/] [grey]Backup created: {backupFile}[/]");
    }

    private string? RenameRepositoryDirectory(string repoPath)
    {
        try
        {
            var tempPath = $"{repoPath}{Guid.NewGuid()}";

            _console.MarkupLineInterpolated($"[grey]Renaming repository folder from {repoPath} to {tempPath}[/]");
            _fileSystem.Directory.Move(repoPath, tempPath);

            return tempPath;
        }
        catch (DirectoryNotFoundException)
        {
            // Directory doesn't exist anymore, which is fine            
        }
        catch (Exception ex)
        {
            _console.MarkupLineInterpolated($"[yellow]⚠[/] [grey]Attempt to rename folder {repoPath} failed: {ex.Message}[/]");
        }

        return null;
    }
}
