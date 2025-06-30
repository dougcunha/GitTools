using System.Diagnostics;
using GitTools.Models;
using Spectre.Console;

namespace GitTools.Tests.Services;

public sealed partial class GitServiceTests
{
    [Fact]
    public async Task FetchAsync_WithoutPrune_ShouldExecuteBasicFetchCommand()
    {
        // Arrange
        const string GIT_FETCH_OUTPUT = "";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_FETCH_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.FetchAsync(REPO_PATH);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "fetch --all --tags" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task FetchAsync_WithPrune_ShouldExecuteFetchCommandWithPruneFlag()
    {
        // Arrange
        const string GIT_FETCH_OUTPUT = "";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_FETCH_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.FetchAsync(REPO_PATH, prune: true);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "fetch --all --tags --prune" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task FetchAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.FetchAsync(NON_EXISTENT_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task FetchAsync_WithEmptyRepositoryPath_ShouldReturnFalse()
    {
        // Arrange
        const string EMPTY_REPO_PATH = "";

        // Act
        var result = await _gitService.FetchAsync(EMPTY_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task FetchAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git fetch failed";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.FetchAsync(REPO_PATH);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error fetching updates");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WithUntrackedBranchAndPushNewBranchesFalse_ShouldReturnTrueWithoutExecutingCommands()
    {
        // Arrange
        const string BRANCH_NAME = "feature-branch";
        var untrackedBranch = new BranchStatus(REPO_PATH, BRANCH_NAME, null, false, 0, 0, false, false, DateTime.Now, true);

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        // Act
        var result = await _gitService.SynchronizeBranchAsync(untrackedBranch, pushNewBranches: false);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WithUntrackedBranchAndPushNewBranchesTrue_ShouldExecutePushUpstreamCommand()
    {
        // Arrange
        const string BRANCH_NAME = "feature-branch";
        const string GIT_PUSH_OUTPUT = "";
        var untrackedBranch = new BranchStatus(REPO_PATH, BRANCH_NAME, null, false, 0, 0, false, false, DateTime.Now, true);

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_PUSH_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.SynchronizeBranchAsync(untrackedBranch, pushNewBranches: true);

        // Assert
        result.ShouldBeTrue();
        _console.Output.ShouldContain($"Pushing new branch {BRANCH_NAME} to remote");

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"push --set-upstream origin {BRANCH_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WithTrackedBranch_ShouldExecuteFullSynchronizationSequence()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string UPSTREAM = "origin/main";
        const string GIT_OUTPUT = "";
        var trackedBranch = new BranchStatus(REPO_PATH, BRANCH_NAME, UPSTREAM, true, 1, 2, false, false, DateTime.Now, true);

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.SynchronizeBranchAsync(trackedBranch);

        // Assert
        result.ShouldBeTrue();

        // Verify all three commands were executed in sequence
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.Arguments == $"branch --quiet --set-upstream-to=origin/{BRANCH_NAME} {BRANCH_NAME}"),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.Arguments == $"checkout {BRANCH_NAME}"),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.Arguments == $"rebase --autostash origin/{BRANCH_NAME}"),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        var branch = new BranchStatus(NON_EXISTENT_REPO_PATH, BRANCH_NAME, "origin/main", true, 0, 0, false, false, DateTime.Now, true);

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.SynchronizeBranchAsync(branch);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WithEmptyRepositoryPath_ShouldReturnFalse()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string EMPTY_REPO_PATH = "";
        var branch = new BranchStatus(EMPTY_REPO_PATH, BRANCH_NAME, "origin/main", true, 0, 0, false, false, DateTime.Now, true);

        // Act
        var result = await _gitService.SynchronizeBranchAsync(branch);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string UPSTREAM = "origin/main";
        const string ERROR_MESSAGE = "Git synchronization failed";
        var trackedBranch = new BranchStatus(REPO_PATH, BRANCH_NAME, UPSTREAM, true, 0, 0, false, false, DateTime.Now, true);

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.SynchronizeBranchAsync(trackedBranch);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain($"Error synchronizing branch {BRANCH_NAME}");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenSuccessful_ShouldExecuteExpectedCommands()
    {
        // Arrange
        var branch = new BranchStatus(REPO_PATH, "main", "origin/main", true, 0, 0, false, false, DateTime.Now, true);
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, false, [branch]);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var checkoutMainCalled = false;
        var rebaseAutoStashCalled = false;
        var setUpstreamCalled = false;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        _processRunner.WhenForAnyArgs
        (
            ctx => ctx.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
        ).Do
        (
            info =>
            {
                var startInfo = info.Arg<ProcessStartInfo>();

                if (startInfo.Arguments.StartsWith("checkout main", StringComparison.Ordinal))
                    checkoutMainCalled = true;
                else if (startInfo.Arguments.StartsWith("rebase --autostash origin/main", StringComparison.Ordinal))
                    rebaseAutoStashCalled = true;
                else if (startInfo.Arguments.StartsWith("branch --quiet --set-upstream-to=origin/main", StringComparison.Ordinal))
                    setUpstreamCalled = true;
            }
        );

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, _ => { });

        // Assert
        result.ShouldBeTrue();
        checkoutMainCalled.ShouldBeTrue();
        rebaseAutoStashCalled.ShouldBeTrue();
        setUpstreamCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, false, []);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(false);
        var progressCalled = false;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, _ => progressCalled = true);

        // Assert
        result.ShouldBeFalse();
        progressCalled.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenRepoPathIsNullOrWhitespace_ShouldReturnFalse()
    {
        // Arrange
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, null!, REMOTE_URL, false, []);
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(false);
        var progressCalled = false;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, _ => progressCalled = true);

        // Assert
        result.ShouldBeFalse();
        progressCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenNoLocalBranches_ShouldReturnFalseAndReport()
    {
        // Arrange
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, false, []);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        string? progressMsg = null;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, msg => progressMsg = msg.ToString());

        // Assert
        result.ShouldBeFalse();
        progressMsg.ShouldNotBeNull();
        progressMsg.ShouldContain("no local branches");
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenExceptionOccurs_ShouldReturnFalseAndReport()
    {
        // Arrange
        var repo = new GitRepositoryStatus
        (
            REPO_NAME,
            REPO_NAME,
            REPO_PATH,
            REMOTE_URL,
            false,
            [new(REPO_PATH, "main", "origin/main", true, 0, 0, false, false, DateTime.Now, true)]
        );

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(_ => throw new InvalidOperationException("sync error"));

        string? progressMsg = null;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, msg => progressMsg = msg.ToString());

        // Assert
        result.ShouldBeFalse();
        progressMsg.ShouldNotBeNull();
        progressMsg.ShouldContain("failed");
        _console.Output.ShouldContain("sync error");
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WithUncommittedChanges_ShouldStashAndPop()
    {
        // Arrange
        var branch = new BranchStatus(REPO_PATH, "main", "origin/main", true, 0, 0, false, false, DateTime.Now, true);
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, true, [branch]);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var stashCalled = false;
        var stashPopCalled = false;

        _processRunner.WhenForAnyArgs
        (
            ctx => ctx.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
        ).Do
        (
            info =>
            {
                var startInfo = info.Arg<ProcessStartInfo>();

                if (startInfo.Arguments.StartsWith("stash pop", StringComparison.Ordinal))
                    stashPopCalled = true;
                else if (startInfo.Arguments.StartsWith("stash", StringComparison.Ordinal))
                    stashCalled = true;
            }
        );

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        string? progressMsg = null;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, msg => progressMsg = msg.ToString(), withUncommited: true);

        // Assert
        result.ShouldBeTrue();
        stashCalled.ShouldBeTrue();
        stashPopCalled.ShouldBeTrue();
        progressMsg.ShouldNotBeNull();
        progressMsg.ShouldContain("updated");
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenBranchNotTrackedAndPushNewBranchesFalse_ShouldNotReportFailure()
    {
        // Arrange
        var branch = new BranchStatus(REPO_PATH, "feature", null, true, 0, 0, false, false, DateTime.Now, true);
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, false, [branch]);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        string? progressMsg = null;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, msg => progressMsg = msg.ToString(), pushNewBranches: false);

        // Assert
        result.ShouldBeTrue(); // O método não retorna false, mas reporta falha na branch
        progressMsg.ShouldNotBeNull();
        progressMsg.ShouldNotContain("failed to synchronize branch");
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenBranchTracked_ShouldSynchronizeAndReportSuccess()
    {
        // Arrange
        var branch = new BranchStatus(REPO_PATH, "main", "origin/main", true, 0, 0, false, false, DateTime.Now, true);
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, false, [branch]);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        string? progressMsg = null;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, msg => progressMsg = msg.ToString());

        // Assert
        result.ShouldBeTrue();
        progressMsg.ShouldNotBeNull();
        progressMsg.ShouldContain("updated");
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenOtherExceptionOccurs_ShouldReturnFalseAndReport()
    {
        // Arrange
        var repoStatus = new GitRepositoryStatus
        (
            "repo",
            "repo",
            REPO_PATH,
            REMOTE_URL,
            false,
            []
        );

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var shouldThrow = true;

        void Progress(FormattableString msg)
        {
            if (!shouldThrow)
            {
                _console.MarkupLineInterpolated(msg);

                return;
            }

            shouldThrow = false;

            throw new InvalidOperationException("Unexpected error");
        }

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repoStatus, Progress);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Unexpected error");
    }

    [Fact]
    public async Task StashAsync_WithoutIncludeUntracked_ShouldExecuteBasicStashCommand()
    {
        // Arrange
        const string GIT_STASH_OUTPUT = "";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_STASH_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.StashAsync(REPO_PATH);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "stash" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task StashAsync_WithIncludeUntracked_ShouldExecuteStashCommandWithUntrackedFlag()
    {
        // Arrange
        const string GIT_STASH_OUTPUT = "";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_STASH_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.StashAsync(REPO_PATH, includeUntracked: true);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "stash --include-untracked" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task StashAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.StashAsync(NON_EXISTENT_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task StashAsync_WithEmptyRepositoryPath_ShouldReturnFalse()
    {
        // Arrange
        const string EMPTY_REPO_PATH = "";

        // Act
        var result = await _gitService.StashAsync(EMPTY_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task StashAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git stash failed";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.StashAsync(REPO_PATH);
        // Assert

        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error stashing changes");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }
}
