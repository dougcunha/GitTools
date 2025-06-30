using System.Diagnostics;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using GitTools.Models;
using GitTools.Services;
using GitTools.Tests.Utils;
using Spectre.Console;
using Spectre.Console.Testing;

namespace GitTools.Tests.Services;

public sealed partial class GitServiceTests
{
    [Fact]
    public async Task RunGitCommandAsync_WhenGitDirectoryExists_ShouldUseOriginalPath()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        const string EXPECTED_OUTPUT = "On branch main";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);

        _fileSystem.Directory.Exists(gitPath).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(EXPECTED_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        result.ShouldContain(EXPECTED_OUTPUT);

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == GIT_ARGUMENTS &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task RunGitCommandAsync_WithLogGitCommandsOption_ShouldWriteGitCommandsToConsole()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);
        _fileSystem.Directory.Exists(gitPath).Returns(true);
        _options.LogAllGitCommands = true;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        _ = await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        _console.Output.ShouldContain($"{REPO_PATH}> git {GIT_ARGUMENTS}");
    }

    [Fact]
    public async Task RunGitCommandAsync_WithNoLogGitCommandsOption_ShouldNotWriteGitCommandsToConsole()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);
        _fileSystem.Directory.Exists(gitPath).Returns(true);
        _options.LogAllGitCommands = false;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        _ = await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        _console.Output.ShouldNotContain($"{REPO_PATH}> git {GIT_ARGUMENTS}");
    }

    [Fact]
    public async Task RunGitCommandAsync_WhenGitWorktree_ShouldUseMainRepoPath()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        const string MAIN_REPO_PATH = @"C:\main\repo\.git\worktrees\feature";
        const string GIT_WORKTREE_CONTENT = $"gitdir: {MAIN_REPO_PATH}";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);

        _fileSystem.Directory.Exists(gitPath).Returns(false);
        _fileSystem.File.Exists(gitPath).Returns(true);

        // ReSharper disable once MethodHasAsyncOverload
        _fileSystem.File.ReadAllText(gitPath).Returns(GIT_WORKTREE_CONTENT);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.WorkingDirectory == Path.GetFullPath(Path.Combine(REPO_PATH, MAIN_REPO_PATH))),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task RunGitCommandAsync_WhenWorkTreeWithNoPrefix_ShouldReturnRepoPath()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        const string MAIN_REPO_PATH = @"C:\main\repo\.git\worktrees\feature";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);

        _fileSystem.Directory.Exists(gitPath).Returns(false);
        _fileSystem.File.Exists(gitPath).Returns(true);

        // ReSharper disable once MethodHasAsyncOverload
        _fileSystem.File.ReadAllText(gitPath).Returns(MAIN_REPO_PATH);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        var res = await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        res.ShouldBeEmpty(); // No output expected since we are not capturing output in this test

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi => psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task RunGitCommandAsync_WhenGitCommandFails_ShouldThrowException()
    {
        // Arrange
        const string GIT_ARGUMENTS = "invalid-command";
        const string ERROR_MESSAGE = "git: 'invalid-command' is not a git command";

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var errorHandler = callInfo.ArgAt<DataReceivedEventHandler>(2);
                errorHandler?.Invoke(null!, CreateDataReceivedEventArgs(ERROR_MESSAGE));

                return 1;
            });

        // Act & Assert
        var exception = await Should.ThrowAsync<Exception>(
            () => _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS));

        exception.Message.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task RunGitCommandAsync_ShouldConfigureProcessCorrectly()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        var fileSystem = new MockFileSystem();
        var processRunner = Substitute.For<IProcessRunner>();
        var console = Substitute.For<IAnsiConsole>();
        var gitService = new GitService(fileSystem, processRunner, console, new GitToolsOptions());
        fileSystem.Directory.CreateDirectory(REPO_PATH);
        fileSystem.Directory.CreateDirectory(Path.Combine(REPO_PATH, GIT_DIR));

        processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        await processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == GIT_ARGUMENTS &&
                psi.WorkingDirectory == REPO_PATH &&
                psi.RedirectStandardOutput &&
                psi.RedirectStandardError &&
                !psi.UseShellExecute &&
                psi.CreateNoWindow),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }
}
