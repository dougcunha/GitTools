using System.Diagnostics;

namespace GitTools.Tests.Services;

public sealed partial class GitServiceTests
{
    [Fact]
    public async Task HasUncommittedChangesAsync_WithUncommittedChanges_ShouldReturnTrue()
    {
        // Arrange
        const string GIT_OUTPUT = "M modified_file.txt\nA new_file.txt";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.HasUncommittedChangesAsync(REPO_PATH);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "status --porcelain" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WithNoChanges_ShouldReturnFalse()
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

                return 0;
            });

        // Act
        var result = await _gitService.HasUncommittedChangesAsync(REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "status --porcelain" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git command failed";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.HasUncommittedChangesAsync(REPO_PATH);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error checking uncommitted changes");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.HasUncommittedChangesAsync(NON_EXISTENT_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WithAheadAndBehindCommits_ShouldReturnCorrectCounts()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string GIT_FETCH_OUTPUT = "";
        const string GIT_COUNT_OUTPUT = "2\t3";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                if (psi.Arguments.Contains("fetch"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_FETCH_OUTPUT));
                }
                else if (psi.Arguments.Contains("rev-list"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_COUNT_OUTPUT));
                }

                return 0;
            });

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ahead.ShouldBe(2);
        result.behind.ShouldBe(3);

        await _processRunner.Received(2).RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WithNoCommitDifferences_ShouldReturnZeros()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string GIT_FETCH_OUTPUT = "";
        const string GIT_COUNT_OUTPUT = "0\t0";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                if (psi.Arguments.Contains("fetch"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_FETCH_OUTPUT));
                }
                else if (psi.Arguments.Contains("rev-list"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_COUNT_OUTPUT));
                }

                return 0;
            });

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ahead.ShouldBe(0);
        result.behind.ShouldBe(0);
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WithFetchDisabled_ShouldSkipFetchCommand()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string GIT_COUNT_OUTPUT = "1\t0";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_COUNT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(REPO_PATH, BRANCH_NAME, fetch: false);

        // Assert
        result.ahead.ShouldBe(1);
        result.behind.ShouldBe(0);

        await _processRunner.Received(1).RunAsync(
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments.Contains("rev-list") &&
                !psi.Arguments.Contains("fetch")),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WhenRepositoryDoesNotExist_ShouldReturnZeros()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(NON_EXISTENT_REPO_PATH, BRANCH_NAME);

        // Assert
        result.ahead.ShouldBe(0);
        result.behind.ShouldBe(0);

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WhenGitCommandFails_ShouldReturnZerosAndLogError()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string ERROR_MESSAGE = "Git command failed";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ahead.ShouldBe(0);
        result.behind.ShouldBe(0);
        _console.Output.ShouldContain("Error getting remote ahead/behind count");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WithInvalidParseData_ShouldReturnZeros()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string GIT_FETCH_OUTPUT = "";
        const string INVALID_GIT_COUNT_OUTPUT = "invalid\tdata";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                if (psi.Arguments.Contains("fetch"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_FETCH_OUTPUT));
                }
                else if (psi.Arguments.Contains("rev-list"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(INVALID_GIT_COUNT_OUTPUT));
                }

                return 0;
            });

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ahead.ShouldBe(0);
        result.behind.ShouldBe(0);
    }
}
