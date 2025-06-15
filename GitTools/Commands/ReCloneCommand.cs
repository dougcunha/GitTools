using System.CommandLine;
using System.Diagnostics;
using System.Text;
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
    private readonly IProcessRunner _processRunner;
    private readonly IBackupService _backupService;
    private readonly IAnsiConsole _console;

    public ReCloneCommand
    (
        IFileSystem fileSystem,
        IProcessRunner processRunner,
        IBackupService backupService,
        IAnsiConsole console
    )
        : base("reclone", "Reclones the git repository in the current directory.")
    {
        _fileSystem = fileSystem;
        _processRunner = processRunner;
        _backupService = backupService;
        _console = console;

        var noBackupOption = new Option<bool>("--no-backup", "Do not create a backup zip of the folder");
        var forceOption = new Option<bool>("--force", "Ignore uncommitted changes to the repository");

        AddOption(noBackupOption);
        AddOption(forceOption);

        this.SetHandler(ExecuteAsync, noBackupOption, forceOption);
    }

    /// <summary>
    /// Executes the reclone operation.
    /// </summary>
    public async Task ExecuteAsync(bool noBackup, bool force)
    {
        var repoPath = _fileSystem.Directory.GetCurrentDirectory();
        var gitPath = _fileSystem.Path.Combine(repoPath, ".git");

        if (!_fileSystem.Directory.Exists(gitPath) && !_fileSystem.File.Exists(gitPath))
        {
            _console.MarkupLine("[red]Current directory is not a git repository.[/]");
            return;
        }

        if (!force)
        {
            var status = await RunGitAsync(repoPath, "status --porcelain");
            if (!string.IsNullOrWhiteSpace(status))
            {
                _console.MarkupLine("[red]Uncommitted changes detected. Use --force to ignore.[/]");
                return;
            }
        }

        var remoteUrl = (await RunGitAsync(repoPath, "config --get remote.origin.url")).Trim();
        var parentDir = _fileSystem.Path.GetDirectoryName(repoPath)!;
        var repoName = _fileSystem.Path.GetFileName(repoPath.TrimEnd(_fileSystem.Path.DirectorySeparatorChar, _fileSystem.Path.AltDirectorySeparatorChar));

        if (!noBackup)
        {
            var backupFile = _fileSystem.Path.Combine(parentDir, $"{repoName}-backup.zip");
            _backupService.CreateBackup(repoPath, backupFile);
            _console.MarkupLineInterpolated($"[grey]Backup created: {backupFile}[/]");
        }

        _fileSystem.Directory.Delete(repoPath, true);
        await _processRunner.RunAsync
        (
            new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"clone {remoteUrl} {repoName}",
                WorkingDirectory = parentDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        ).ConfigureAwait(false);

        _console.MarkupLine("[green]Repository recloned successfully.[/]");
    }

    private async Task<string> RunGitAsync(string workingDirectory, string arguments)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        var exitCode = await _processRunner.RunAsync
        (
            new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); },
            (_, e) => { if (e.Data is not null) error.AppendLine(e.Data); }
        ).ConfigureAwait(false);

        return exitCode != 0 ? throw new InvalidOperationException(error.ToString()) : output.ToString();
    }
}
