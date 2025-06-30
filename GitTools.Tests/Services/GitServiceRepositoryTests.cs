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
    public async Task GetGitRepositoryAsync_WhenValidRepository_ShouldReturnValidGitRepository()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var processRunner = Substitute.For<IProcessRunner>();
        var console = Substitute.For<IAnsiConsole>();
        var gitService = new GitService(fileSystem, processRunner, console, new GitToolsOptions());

        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);

        fileSystem.Directory.SetCurrentDirectory(CURRENT_DIRECTORY);
        fileSystem.Directory.CreateDirectory(REPO_NAME);
        fileSystem.Directory.CreateDirectory(gitPath);

        processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(REMOTE_URL));

                return Task.FromResult(0);
            });

        // Act
        var result = await gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBe(REMOTE_URL);
        result.IsValid.ShouldBeTrue();

        await processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == "config --get remote.origin.url" &&
                psi.WorkingDirectory == REPO_NAME),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenRepositoryNotExists_ShouldReturnInvalidGitRepository()
    {
        // Arrange

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(false);

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBeNull();
        result.IsValid.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>());
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenGitDirectoryNotExists_ShouldReturnInvalidGitRepository()
    {
        // Arrange
        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(true);
        _fileSystem.Directory.Exists(gitPath).Returns(false);
        _fileSystem.File.Exists(gitPath).Returns(false);

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBeNull();
        result.IsValid.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>());
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenGitCommandFails_ShouldReturnInvalidGitRepository()
    {
        // Arrange
        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(true);
        _fileSystem.Directory.Exists(gitPath).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var errorHandler = callInfo.ArgAt<DataReceivedEventHandler>(2);
                errorHandler?.Invoke(null!, CreateDataReceivedEventArgs("fatal: not a git repository"));

                return Task.FromResult(1);
            });

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBeNull();
        result.IsValid.ShouldBeFalse();
    }


    [Fact]
    public async Task GetGitRepositoryAsync_WhenGitCommandFailsButConfigFileExists_ShouldReturnRepositoryWithUrlFromConfig()
    {
        // Arrange
        const string CONFIG_CONTENT = """
            [core]
                repositoryformatversion = 0
                filemode = true
                bare = false
            [remote "origin"]
                url = https://github.com/user/config-repo.git
                fetch = +refs/heads/*:refs/remotes/origin/*
            """;

        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);
        var configPath = Path.Combine(REPO_NAME, GIT_DIR, "config");

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(true);
        _fileSystem.Directory.Exists(gitPath).Returns(true);
        _fileSystem.File.Exists(configPath).Returns(true);

        _fileSystem.File.ReadLinesAsync(configPath).Returns(MockReadLinesAsync(CONFIG_CONTENT));

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var errorHandler = callInfo.ArgAt<DataReceivedEventHandler>(2);
                errorHandler?.Invoke(null!, CreateDataReceivedEventArgs("fatal: unable to read config"));

                return Task.FromResult(1);
            });

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBe("https://github.com/user/config-repo.git");
        result.IsValid.ShouldBeTrue();
        result.HasErrors.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == "config --get remote.origin.url" &&
                psi.WorkingDirectory == REPO_NAME),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        _fileSystem.File.Received(1).ReadLinesAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenGitCommandFailsButConfigFileExistsWithNoRemote_ShouldReturnRepositoryWithoutUrl()
    {
        // Arrange
        const string CONFIG_CONTENT = """
        [core]
            repositoryformatversion = 0
            filemode = true
            bare = false
        [remote "origin"]
            fetch = +refs/heads/*:refs/remotes/origin/*
        """;

        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);
        var configPath = Path.Combine(REPO_NAME, GIT_DIR, "config");

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(true);
        _fileSystem.Directory.Exists(gitPath).Returns(true);
        _fileSystem.File.Exists(configPath).Returns(true);

        _fileSystem.File.ReadLinesAsync(configPath).Returns(MockReadLinesAsync(CONFIG_CONTENT));

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var errorHandler = callInfo.ArgAt<DataReceivedEventHandler>(2);
                errorHandler?.Invoke(null!, CreateDataReceivedEventArgs("fatal: unable to read config"));

                return Task.FromResult(1);
            });

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBeNull();
        result.IsValid.ShouldBeFalse();
        result.HasErrors.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == "config --get remote.origin.url" &&
                psi.WorkingDirectory == REPO_NAME),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        _fileSystem.File.Received(1).ReadLinesAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenGitCommandFailsAndConfigFileNotExists_ShouldReturnRepositoryWithoutUrl()
    {
        // Arrange
        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);
        var configPath = Path.Combine(REPO_NAME, GIT_DIR, "config");

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(true);
        _fileSystem.Directory.Exists(gitPath).Returns(true);
        _fileSystem.File.Exists(configPath).Returns(false);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var errorHandler = callInfo.ArgAt<DataReceivedEventHandler>(2);
                errorHandler?.Invoke(null!, CreateDataReceivedEventArgs("fatal: unable to read config"));

                return Task.FromResult(1);
            });

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBeNull();
        result.IsValid.ShouldBeFalse();
        result.HasErrors.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>
            (
                static psi =>
                    psi.FileName == "git" &&
                    psi.Arguments == "config --get remote.origin.url" &&
                    psi.WorkingDirectory == REPO_NAME),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        _fileSystem.File.DidNotReceive().ReadLinesAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_ShouldUseAppropriatedCommand()
    {
        // Arrange
        const string REPOSITORY_PATH = "C:/test/repo";
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        _fileSystem.Directory.Exists(REPOSITORY_PATH).Returns(false);

        // Act
        var result = await _gitService.DeleteLocalGitRepositoryAsync(REPOSITORY_PATH);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>
            (
                psi =>
                    psi.FileName.Contains(isWindows ? "powershell" : "bash") &&
                    psi.Arguments.Contains(isWindows ? $"Remove-Item -Recurse -Force '{REPOSITORY_PATH}'" : "rm -rf")
            ),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_WhenPathIsNull_ShouldReturnFalse()
    {
        // Act
        var result = await _gitService.DeleteLocalGitRepositoryAsync(null);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>());
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_WhenPathIsEmpty_ShouldReturnFalse()
    {
        // Act
        var result = await _gitService.DeleteLocalGitRepositoryAsync(string.Empty);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>());
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_WhenCommandFails_ShouldReturnFalse()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\test\repo";
        var fileSystem = new MockFileSystem();
        var processRunner = Substitute.For<IProcessRunner>();
        var console = new TestConsole();
        var gitService = new GitService(fileSystem, processRunner, console, new GitToolsOptions());
        fileSystem.Directory.CreateDirectory(REPOSITORY_PATH);

        processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(1);

        // Act
        var result = await gitService.DeleteLocalGitRepositoryAsync(REPOSITORY_PATH);

        // Assert
        result.ShouldBeFalse();

        console.Output.ShouldContain($"Error deleting repository {REPOSITORY_PATH}: result code 1");
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_WhenExceptionOccurs_ShouldReturnFalse()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\test\repo";
        const string ERROR_MESSAGE = "Access denied";
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(REPOSITORY_PATH);
        var processRunner = Substitute.For<IProcessRunner>();
        var console = new TestConsole();
        var gitService = new GitService(fileSystem, processRunner, console, new GitToolsOptions());

        processRunner.When(static x => x.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>()))
            .Do(static _ => throw new Exception(ERROR_MESSAGE));

        // Act
        var result = await gitService.DeleteLocalGitRepositoryAsync(REPOSITORY_PATH);

        // Assert
        result.ShouldBeFalse();

        console.Output.ShouldContain($"Error deleting repository {REPOSITORY_PATH}: {ERROR_MESSAGE}");
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_WhenDirectoryStillExists_ShouldReturnFalse()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\test\repo";

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        _fileSystem.Directory.Exists(REPOSITORY_PATH).Returns(true);

        // Act
        var result = await _gitService.DeleteLocalGitRepositoryAsync(REPOSITORY_PATH);

        // Assert
        result.ShouldBeFalse();
    }
}
