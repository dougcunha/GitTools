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

    public ReCloneCommand
    (
        IFileSystem fileSystem,
        IBackupService backupService,
        IGitService gitService,
        IAnsiConsole console
    )
        : base("reclone", "Reclones the specified git repository.")
    {
        _fileSystem = fileSystem;
        _backupService = backupService;
        _gitService = gitService;
        _console = console;

        var repositoryPathArgument = new Argument<string>
        (
            "repository-path",
            "Path to the git repository to reclone"
        );

        var noBackupOption = new Option<bool>("--no-backup", "Do not create a backup zip of the folder");
        var forceOption = new Option<bool>("--force", "Ignore uncommitted changes to the repository");

        AddArgument(repositoryPathArgument);
        AddOption(noBackupOption);
        AddOption(forceOption);

        this.SetHandler(ExecuteAsync, repositoryPathArgument, noBackupOption, forceOption);
    }

    /// <summary>
    /// Executes the reclone operation.
    /// </summary>
    public async Task ExecuteAsync(string repositoryPath, bool noBackup, bool force)
    {
        var repo = await _gitService.GetGitRepositoryAsync(repositoryPath).ConfigureAwait(false);

        var safeRepoPath = repo.Path;
        var safeRepoName = repo.Name;
        var safeParentDir = Path.GetDirectoryName(safeRepoPath) ?? string.Empty;
        var repoPathMsg = repo.Path;

        if (!repo.IsValid && !_fileSystem.Directory.Exists(safeParentDir))
        {
            _console.MarkupLineInterpolated($"[red]{repositoryPath} is not valid or does not exist: {repoPathMsg}[/]");

            return;
        }

        _console.MarkupLineInterpolated($"[grey]Recloning repository: {safeRepoName} at {safeRepoPath}[/]");

        if (!force)
        {
            if (repo.HasErrors)
            {
                _console.MarkupLine("[red]Repository has errors and cannot be inspected for changes. If you want to reclone, use the --force option to ignore local changes.[/]");

                return;
            }

            var status = await _gitService.RunGitCommandAsync(safeRepoPath, "status --porcelain").ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(status))
            {
                _console.MarkupLine("[red]Uncommitted changes detected. Use --force to ignore.[/]");

                return;
            }
        }

        if (!noBackup)
            GenerateRepositoryBackup(safeRepoPath, safeRepoName);

        var tempPath = RenameRepositoryDirectory(safeRepoPath);

        await _console.Status()
            .StartAsync
            (
                "[yellow]Cloning repository...[/]",
                _ => _gitService.RunGitCommandAsync(safeParentDir, $"clone {repo.RemoteUrl} {safeRepoName}")
            ).ConfigureAwait(false);

        await DeleteOldRepositoryAsync(tempPath).ConfigureAwait(false);

        _console.MarkupLine("[green]✓ Repository recloned successfully.[/]");
    }

    private async Task DeleteOldRepositoryAsync(string? tempPath)
    {
        if (string.IsNullOrWhiteSpace(tempPath))
            return;

        var deleteOldRepositoryResult = await _gitService
            .DeleteLocalGitRepositoryAsync(tempPath)
            .ConfigureAwait(false);

        if (deleteOldRepositoryResult)
            _console.MarkupLineInterpolated($"[green]✓[/] [grey]Old repository deleted: {tempPath}[/]");
    }

    private void GenerateRepositoryBackup(string repoPath, string repoName)
    {
        var backupDir = GetDirectory(repoPath);

        var backupFile = CombinePath(backupDir, $"{repoName}-backup.zip");

        _console.Status()
            .Start("[yellow]Creating backup...[/]", _ => _backupService.CreateBackup(repoPath, backupFile));

        _console.MarkupLineInterpolated($"[green]✓[/] [grey]Backup created: {backupFile}[/]");
    }

    private static string CombinePath(string directory, string fileName)
    {
        var separator = directory.Contains('\\')
            ? '\\'
            : Path.DirectorySeparatorChar;

        directory = directory.TrimEnd('\n', '\r', '\\', '/');

        return string.Concat(directory, separator, fileName);
    }

    private static string GetDirectory(string path)
    {
        var containsBackslash = path.Contains('\\');
        path = path.Replace('\\', Path.DirectorySeparatorChar);
        var dir = Path.GetDirectoryName(path) ?? string.Empty;

        return containsBackslash
            ? dir.Replace(Path.DirectorySeparatorChar, '\\')
            : dir;
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
