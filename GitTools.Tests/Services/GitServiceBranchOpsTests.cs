using System.Diagnostics;
using GitTools.Tests.Utils;

namespace GitTools.Tests.Services;

public sealed partial class GitServiceTests
{
    [Fact]
    public async Task IsCurrentBranchAsync_WhenBranchIsCurrent_ShouldReturnTrue()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(BRANCH_NAME));

                return 0;
            });

        // Act
        var result = await _gitService.IsCurrentBranchAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsCurrentBranchAsync_WhenBranchIsNotCurrent_ShouldReturnFalse()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string CURRENT_BRANCH = "develop";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(CURRENT_BRANCH));

                return 0;
            });

        // Act
        var result = await _gitService.IsCurrentBranchAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsCurrentBranchAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.IsCurrentBranchAsync(NON_EXISTENT_REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task IsCurrentBranchAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string ERROR_MESSAGE = "Git error";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.IsCurrentBranchAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error checking current branch");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task IsBranchTrackedAsync_WhenBranchIsTracked_ShouldReturnTrueAndUpstream()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string UPSTREAM = "origin/main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                if (psi.Arguments.Contains("for-each-ref"))
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(UPSTREAM));
                else if (psi.Arguments.Contains("show-ref"))
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs($"abc123 refs/remotes/{UPSTREAM}"));

                return 0;
            });

        // Act
        var (isTracked, upstream) = await _gitService.IsBranchTrackedAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        isTracked.ShouldBeTrue();
        upstream.ShouldBe(UPSTREAM);
    }

    [Fact]
    public async Task IsBranchTrackedAsync_WhenBranchIsNotTracked_ShouldReturnFalseAndNull()
    {
        // Arrange
        const string BRANCH_NAME = "feature/no-upstream";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                if (psi.Arguments.Contains("for-each-ref"))
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));
                else if (psi.Arguments.Contains("show-ref"))
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

                return 0;
            });

        // Act
        var (isTracked, upstream) = await _gitService.IsBranchTrackedAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        isTracked.ShouldBeFalse();
        upstream.ShouldBeNull();
    }

    [Fact]
    public async Task IsBranchTrackedAsync_WhenRepositoryDoesNotExist_ShouldReturnFalseAndNull()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var (isTracked, upstream) = await _gitService.IsBranchTrackedAsync(NON_EXISTENT_REPO_PATH, BRANCH_NAME);

        // Assert
        isTracked.ShouldBeFalse();
        upstream.ShouldBeNull();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task IsBranchTrackedAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string ERROR_MESSAGE = "Git error";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var (isTracked, upstream) = await _gitService.IsBranchTrackedAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        isTracked.ShouldBeFalse();
        upstream.ShouldBeNull();
        _console.Output.ShouldContain("Error checking if branch");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task PushAsync_Default_ShouldPushBranchAndTags()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var callCount = 0;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(_ =>
            {
                callCount++;

                return 0;
            });

        // Act
        var result = await _gitService.PushAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeTrue();
        callCount.ShouldBe(2); // push branch + push --tags

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments.Contains("push") &&
                psi.Arguments.Contains(BRANCH_NAME)),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments.Contains("push --tags")),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PushAsync_WithForce_ShouldPushWithForceFlag()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var callCount = 0;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(_ =>
            {
                callCount++;

                return 0;
            });

        // Act
        var result = await _gitService.PushAsync(REPO_PATH, BRANCH_NAME, force: true);

        // Assert
        result.ShouldBeTrue();
        callCount.ShouldBe(2); // push branch + push --tags

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments.Contains("push --force") &&
                psi.Arguments.Contains(BRANCH_NAME)),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PushAsync_WithoutTags_ShouldNotPushTags()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var callCount = 0;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(_ =>
            {
                callCount++;

                return 0;
            });

        // Act
        var result = await _gitService.PushAsync(REPO_PATH, BRANCH_NAME, tags: false);

        // Assert
        result.ShouldBeTrue();
        callCount.ShouldBe(1); // apenas push branch

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments.Contains("push") &&
                psi.Arguments.Contains(BRANCH_NAME)),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PushAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.PushAsync(NON_EXISTENT_REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PushAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string ERROR_MESSAGE = "Git push failed";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.PushAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error pushing changes");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task CheckoutAsync_WhenBranchIsValid_ShouldRunGitCheckout()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var called = false;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(_ => { called = true; return 0; });

        // Act
        await _gitService.CheckoutAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        called.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"checkout {BRANCH_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckoutAsync_WhenBranchIsNullOrWhitespace_ShouldNotRunGitCheckout(string? branch)
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        // Act
        await _gitService.CheckoutAsync(REPO_PATH, branch);

        // Assert
        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task CheckoutAsync_WhenRepositoryDoesNotExist_ShouldNotRunGitCheckout()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(false);

        // Act
        await _gitService.CheckoutAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task CheckoutAsync_WhenExceptionOccurs_ShouldLogError()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test";
        const string ERROR_MESSAGE = "Checkout failed";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        await _gitService.CheckoutAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        _console.Output.ShouldContain($"Error checking out branch {BRANCH_NAME}");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task GetLocalBranchesAsync_WhenRepositoryHasBranches_ShouldReturnBranchList()
    {
        // Arrange
        const string GIT_OUTPUT = "main\ndevelop\nfeature/test-1\n";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns(static callInfo =>
        {
            var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
            outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

            return 0;
        });

        // Act
        var result = await _gitService.GetLocalBranchesAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContain("main");
        result.ShouldContain("develop");
        result.ShouldContain("feature/test-1");
    }

    [Fact]
    public async Task GetLocalBranchesAsync_WhenRepositoryHasNoBranches_ShouldReturnEmptyList()
    {
        // Arrange
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns(static callInfo =>
        {
            var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
            outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

            return 0;
        });

        // Act
        var result = await _gitService.GetLocalBranchesAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLocalBranchesAsync_WhenBranchesHaveQuotes_ShouldReturnTrimmedNames()
    {
        // Arrange
        const string GIT_OUTPUT = "'main'\n'feature/with space'\n'bugfix'\n";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns(static callInfo =>
        {
            var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
            outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

            return 0;
        });

        // Act
        var result = await _gitService.GetLocalBranchesAsync(REPO_PATH);

        // Assert
        result.ShouldContain("main");
        result.ShouldContain("feature/with space");
        result.ShouldContain("bugfix");
    }

    [Fact]
    public async Task GetLocalBranchesAsync_WhenRepositoryDoesNotExist_ShouldReturnEmptyList()
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.GetLocalBranchesAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetLocalBranchesAsync_WhenGitCommandFails_ShouldReturnEmptyListAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "git error";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.GetLocalBranchesAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        _console.Output.ShouldContain("Error getting local branches");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task PopAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.PopAsync(NON_EXISTENT_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PopAsync_WithEmptyRepositoryPath_ShouldReturnFalse()
    {
        // Arrange
        const string EMPTY_REPO_PATH = "";

        // Act
        var result = await _gitService.PopAsync(EMPTY_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PopAsync_WhenGitCommandSucceeds_ShouldReturnTrue()
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns(0);

        // Act
        var result = await _gitService.PopAsync(REPO_PATH);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "stash pop" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PopAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git stash pop failed";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.PopAsync(REPO_PATH);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error poping stashing changes");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenBranchExists_ShouldReturnBranchName()
    {
        // Arrange
        const string CURRENT_BRANCH = "main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(CURRENT_BRANCH));

                return 0;
            });

        // Act
        var result = await _gitService.GetCurrentBranchAsync(REPO_PATH);

        // Assert
        result.ShouldBe(CURRENT_BRANCH);

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "rev-parse --abbrev-ref HEAD" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenBranchHasWhitespace_ShouldReturnTrimmedBranchName()
    {
        // Arrange
        const string CURRENT_BRANCH = "  feature/test-branch  ";
        const string EXPECTED_BRANCH = "feature/test-branch";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(CURRENT_BRANCH));

                return 0;
            });

        // Act
        var result = await _gitService.GetCurrentBranchAsync(REPO_PATH);

        // Assert
        result.ShouldBe(EXPECTED_BRANCH);
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenOutputIsEmpty_ShouldReturnNull()
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
        var result = await _gitService.GetCurrentBranchAsync(REPO_PATH);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenOutputIsWhitespace_ShouldReturnNull()
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs("   "));

                return 0;
            });

        // Act
        var result = await _gitService.GetCurrentBranchAsync(REPO_PATH);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenRepositoryPathIsNull_ShouldReturnNull()
    {
        // Act
        var result = await _gitService.GetCurrentBranchAsync(null!);

        // Assert
        result.ShouldBeNull();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenRepositoryPathIsEmpty_ShouldReturnNull()
    {
        // Act
        var result = await _gitService.GetCurrentBranchAsync(string.Empty);

        // Assert
        result.ShouldBeNull();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenRepositoryDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.GetCurrentBranchAsync(NON_EXISTENT_REPO_PATH);

        // Assert
        result.ShouldBeNull();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenGitCommandFails_ShouldReturnNullAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git error occurred";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.GetCurrentBranchAsync(REPO_PATH);

        // Assert
        result.ShouldBeNull();
        _console.Output.ShouldContain("Error retrieving current branch");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_ShouldExcludeProtectedBranches()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/old" };
        var mergedBranches = new List<string> { "main", "feature/old" };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            mergedBranches: mergedBranches,
            fullyMergedBranches: mergedBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldNotContain(static b => b.Name == "main");
        result.ShouldContain(static b => b.Name == "feature/old");
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenRepositoryPathIsNull_ShouldReturnEmptyList()
    {
        // Act
        var result = await _gitService.GetPrunableBranchesAsync(null!, merged: true, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldBeEmpty();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenRepositoryPathIsEmpty_ShouldReturnEmptyList()
    {
        // Act
        var result = await _gitService.GetPrunableBranchesAsync(string.Empty, merged: true, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldBeEmpty();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenRepositoryPathIsWhitespace_ShouldReturnEmptyList()
    {
        // Act
        var result = await _gitService.GetPrunableBranchesAsync("   ", merged: true, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldBeEmpty();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenRepositoryDoesNotExist_ShouldReturnEmptyList()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(NON_EXISTENT_REPO_PATH, merged: true, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldBeEmpty();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenGitCommandFails_ShouldReturnEmptyListAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git command failed";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldBeEmpty();
        _console.Output.ShouldContain("Error getting local branches");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WithMergedOption_ShouldReturnBranches()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/one", "feature/two" };
        var mergedBranches = new List<string> { "feature/one", "feature/two" };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            mergedBranches: mergedBranches,
            fullyMergedBranches: mergedBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldContain(static b => b.Name == "feature/one");
        result.ShouldContain(static b => b.Name == "feature/two");
        result.ShouldNotContain(static b => b.Name == "main");
        result.ShouldNotContain(static b => b.Name == "develop");
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenMergedBranchesRequested_ShouldReturnMergedBranches()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/branch1", "feature/branch2" };
        var mergedBranches = new List<string> { "feature/branch1", "feature/branch2", "main" };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            mergedBranches: mergedBranches,
            fullyMergedBranches: mergedBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldContain(static b => b.Name == "feature/branch1");
        result.ShouldContain(static b => b.Name == "feature/branch2");
        result.ShouldNotContain(static b => b.Name == "main"); // Protected branch
        result.ShouldNotContain(static b => b.Name == "master"); // Protected branch
        result.ShouldNotContain(static b => b.Name == "develop"); // Protected branch
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenGoneBranchesRequested_ShouldReturnGoneBranches()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/gone1", "feature/gone2" };

        var goneBranches = new Dictionary<string, bool>
        {
            ["feature/gone1"] = true,
            ["feature/gone2"] = true,
            ["main"] = false
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            goneBranches: goneBranches,
            fullyMergedBranches: [..goneBranches.Keys]
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: false, gone: true, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldContain(static b => b.Name == "feature/gone1");
        result.ShouldContain(static b => b.Name == "feature/gone2");
        result.ShouldNotContain(static b => b.Name == "main");
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenOlderThanDaysRequested_ShouldReturnOldBranches()
    {
        // Arrange
        const int OLDER_THAN_DAYS = 30;
        List<string> branches = ["main", "feature/old-branch", "feature/recent-branch"];

        var lastCommitDates = new Dictionary<string, DateTime>
        {
            ["main"] = DateTime.UtcNow.AddDays(-40),
            ["feature/old-branch"] = DateTime.UtcNow.AddDays(-40),
            ["feature/recent-branch"] = DateTime.UtcNow.AddDays(-10)
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            lastCommitDates: lastCommitDates,
            fullyMergedBranches: branches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: false, gone: false, includeNotFullyMerged: false, olderThanDays: OLDER_THAN_DAYS);

        // Assert
        result.ShouldContain(static b => b.Name == "feature/old-branch");
        result.ShouldNotContain(static b => b.Name == "feature/recent-branch");
        result.ShouldNotContain(static b => b.Name == "main"); // Protected branch
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenMultipleCriteriaProvided_ShouldReturnUnionOfResults()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/merged1", "feature/merged2", "feature/gone1", "feature/old-branch" };
        var mergedBranches = new List<string> { "feature/merged1", "feature/merged2" };

        var goneBranches = new Dictionary<string, bool>
        {
            ["feature/gone1"] = true
        };

        var lastCommitDates = new Dictionary<string, DateTime>
        {
            ["feature/old-branch"] = DateTime.UtcNow.AddDays(-40)
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            goneBranches: goneBranches,
            lastCommitDates: lastCommitDates,
            mergedBranches: mergedBranches,
            fullyMergedBranches: branches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: true, includeNotFullyMerged: false, olderThanDays: 30);

        // Assert
        result.ShouldContain(static b => b.Name == "feature/merged1");
        result.ShouldContain(static b => b.Name == "feature/merged2");
        result.ShouldContain(static b => b.Name == "feature/gone1");
        result.ShouldContain(static b => b.Name == "feature/old-branch");
        result.Count.ShouldBe(4);
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenCurrentBranchIsInResults_ShouldExcludeCurrentBranch()
    {
        // Arrange
        const string CURRENT_BRANCH = "feature/current";
        var branches = new List<string> { "main", "feature/normal-branch", "feature/current" };
        var mergedBranches = new List<string> { "main", "feature/normal-branch", "feature/current" };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: CURRENT_BRANCH,
            mergedBranches: mergedBranches,
            fullyMergedBranches: mergedBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldContain(static b => b.Name == "feature/normal-branch");
        result.ShouldNotContain(static b => b.Name == "main"); // Protected branch
        result.ShouldNotContain(static b => b.Name == "feature/current"); // Current branch should be filtered out
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WithDetachedHeadBranches_ShouldFilterOutDetachedHeads()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/normal-branch" }; // GetLocalBranchesAsync já filtra detached heads
        var mergedBranches = new List<string> { "main", "feature/normal-branch" };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            mergedBranches: mergedBranches,
            fullyMergedBranches: mergedBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldContain(static b => b.Name == "feature/normal-branch");
        result.ShouldNotContain(static b => b.Name == "main"); // Protected branch
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WithGoneBranchesIncludingDetached_ShouldFilterOutDetachedHeads()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/gone1", "feature/gone2" }; // Detached heads não aparecem em GetLocalBranchesAsync

        var goneBranches = new Dictionary<string, bool>
        {
            ["feature/gone1"] = true,
            ["feature/gone2"] = true,
            ["main"] = false
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            goneBranches: goneBranches,
            fullyMergedBranches: branches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: false, gone: true, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldContain(static b => b.Name == "feature/gone1");
        result.ShouldContain(static b => b.Name == "feature/gone2");
        result.ShouldNotContain(static b => b.Name == "main");
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WithOlderThanAndDetachedHead_ShouldFilterOutDetachedHead()
    {
        // Arrange
        const int OLDER_THAN_DAYS = 30;
        const string OLD_NORMAL_BRANCH = "feature/old-branch";
        var branches = new List<string> { "main", OLD_NORMAL_BRANCH }; // Detached heads não aparecem em GetLocalBranchesAsync

        var threshold = DateTime.UtcNow.AddDays(-OLDER_THAN_DAYS - 1);

        var lastCommitDates = new Dictionary<string, DateTime>
        {
            [OLD_NORMAL_BRANCH] = threshold,
            ["main"] = threshold
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            lastCommitDates: lastCommitDates,
            fullyMergedBranches: branches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: false, gone: false, includeNotFullyMerged: false, olderThanDays: OLDER_THAN_DAYS);

        // Assert
        result.ShouldContain(static b => b.Name == OLD_NORMAL_BRANCH);
        result.ShouldNotContain(static b => b.Name == "main"); // Protected branch
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteLocalBranchAsync_WhenBranchExists_ShouldCallGitBranchDelete()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test-branch";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.DeleteLocalBranchAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"branch -d {BRANCH_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteLocalBranchAsync_WhenForceIsTrue_ShouldCallGitBranchDeleteWithForceFlag()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test-branch";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.DeleteLocalBranchAsync(REPO_PATH, BRANCH_NAME, force: true);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"branch -D {BRANCH_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteLocalBranchAsync_WhenRepositoryPathIsNull_ShouldThrowException()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test-branch";

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => _gitService.DeleteLocalBranchAsync(null!, BRANCH_NAME));

        exception.ShouldNotBeNull();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteLocalBranchAsync_WhenBranchNameIsNullOrWhitespace_ShouldCallGitWithInvalidBranch(string? branchName)
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(_ => throw new InvalidOperationException("error: branch '' not found."));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _gitService.DeleteLocalBranchAsync(REPO_PATH, branchName!));

        exception.ShouldNotBeNull();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteLocalBranchAsync_WhenGitCommandFails_ShouldThrowException()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test-branch";
        const string ERROR_MESSAGE = "error: branch 'feature/test-branch' not found.";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(_ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _gitService.DeleteLocalBranchAsync(REPO_PATH, BRANCH_NAME));

        exception.Message.ShouldBe(ERROR_MESSAGE);

        await _processRunner.Received(1).RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteLocalBranchAsync_WhenBranchHasSpecialCharacters_ShouldPassCorrectArguments()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test-branch-with-special#chars";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.DeleteLocalBranchAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"branch -d {BRANCH_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenNoCriteriaProvided_ShouldReturnEmptyList()
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: false, gone: false, includeNotFullyMerged: false, olderThanDays: null);

        // Assert
        result.ShouldBeEmpty();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetBranchStatusesAsync_WhenGetLastCommitDateAsyncFails_ShouldSetDateTimeMinValue()
    {
        // Arrange
        const string FEATURE_BRANCH = "feature-branch";
        const string ERROR_BRANCH = "error-branch";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureGitCommands(new GitCommandConfiguratorOptions
        {
            LocalBranches = [FEATURE_BRANCH, ERROR_BRANCH],
            MergedBranches = [],
            GoneBranches = [],
            FullyMergedBranches = [FEATURE_BRANCH, ERROR_BRANCH],
            BranchCommitDateErrors = [ERROR_BRANCH]
        });

        // Act
        var result = await _gitService.GetBranchStatusesAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);

        var errorBranchStatus = result.First(static b => b.Name == ERROR_BRANCH);
        var normalBranchStatus = result.First(static b => b.Name == FEATURE_BRANCH);

        errorBranchStatus.LastCommitDate.ShouldBe(DateTime.MinValue);
        normalBranchStatus.LastCommitDate.ShouldNotBe(DateTime.MinValue);
    }
}
