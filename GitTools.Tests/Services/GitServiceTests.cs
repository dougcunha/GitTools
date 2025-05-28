using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using GitTools.Services;

namespace GitTools.Tests.Services;

[ExcludeFromCodeCoverage]
public sealed class GitServiceTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly GitService _gitService;

    private const string REPO_PATH = @"C:\test\repo";
    private const string TAG_NAME = "v1.0.0";
    private const string GIT_DIR = ".git";

    public GitServiceTests()
        => _gitService = new GitService(_fileSystem, _processRunner);

    private static DataReceivedEventArgs CreateDataReceivedEventArgs(string data)
    {
        var constructor = typeof(DataReceivedEventArgs)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(string)], null);

        return (DataReceivedEventArgs)constructor!.Invoke([data]);
    }

    [Fact]
    public async Task HasTagAsync_WhenTagExists_ShouldReturnTrue()
    {
        // Arrange
        const string GIT_OUTPUT = "v1.0.0";

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.HasTagAsync(REPO_PATH, TAG_NAME);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"tag -l {TAG_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task HasTagAsync_WhenTagDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

                return 0;
            });

        // Act
        var result = await _gitService.HasTagAsync(REPO_PATH, TAG_NAME);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteTagAsync_ShouldCallGitTagDelete()
    {
        // Arrange
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.DeleteTagAsync(REPO_PATH, TAG_NAME);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"tag -d {TAG_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteRemoteTagAsync_ShouldCallGitPushOriginDelete()
    {
        // Arrange
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.DeleteRemoteTagAsync(REPO_PATH, TAG_NAME);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"push origin :refs/tags/{TAG_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

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
    public async Task RunGitCommandAsync_WhenGitWorktree_ShouldUseMainRepoPath()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        const string MAIN_REPO_PATH = @"C:\main\repo\.git\worktrees\feature";
        const string GIT_WORKTREE_CONTENT = $"gitdir: {MAIN_REPO_PATH}";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);

        _fileSystem.Directory.Exists(gitPath).Returns(false);
        _fileSystem.File.Exists(gitPath).Returns(true);

        #pragma warning disable S6966
        // ReSharper disable once MethodHasAsyncOverload
        _fileSystem.File.ReadAllText(gitPath).Returns(GIT_WORKTREE_CONTENT);
        #pragma warning restore S6966

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

        #pragma warning disable S6966
        // ReSharper disable once MethodHasAsyncOverload
        _fileSystem.File.ReadAllText(gitPath).Returns(MAIN_REPO_PATH);
        #pragma warning restore S6966

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

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        await _processRunner.Received(1).RunAsync
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
