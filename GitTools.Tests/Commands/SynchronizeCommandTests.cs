using System.CommandLine;
using GitTools.Commands;
using GitTools.Models;
using GitTools.Services;
using NSubstitute.ExceptionExtensions;
using Spectre.Console.Testing;

namespace GitTools.Tests.Commands;

[ExcludeFromCodeCoverage]
public sealed class SynchronizeCommandTests
{
    private static string _rootDirectory = FileSystemUtils.GetNormalizedPathForCurrentPlatform("C:/repos");
    private static string _repoPath = FileSystemUtils.GetNormalizedPathForCurrentPlatform("C:/repos/repo1");
    private const string REPO_NAME = "repo1";
    private static readonly List<BranchStatus> _syncedBranches = [new(_repoPath, "main", "origin/main", true, 0, 0)];
    private static readonly List<BranchStatus> _outdatedBranches = [new(_repoPath, "main", "origin/main", true, 1, 0)];
    private readonly IGitRepositoryScanner _mockScanner = Substitute.For<IGitRepositoryScanner>();
    private readonly IGitService _mockGitService = Substitute.For<IGitService>();
    private readonly TestConsole _testConsole = new();
    private readonly IConsoleDisplayService _mockDisplayService = Substitute.For<IConsoleDisplayService>();
    private readonly SynchronizeCommand _command;

    public SynchronizeCommandTests()
    {
        _testConsole.Interactive();
        _command = new SynchronizeCommand(_mockScanner, _mockGitService, _testConsole, _mockDisplayService);
    }

    [Fact]
    public void Constructor_ShouldSetCorrectNameAndDescription()
    {
        _command.Name.ShouldBe("sync");
        _command.Description.ShouldBe("Checks and synchronize repositories with the remote server.");
    }

    [Fact]
    public void Constructor_ShouldConfigureArgumentsAndOptions()
    {
        _command.Arguments.Count.ShouldBe(1);
        _command.Options.Count.ShouldBe(6);

        var rootArg = _command.Arguments[0];
        rootArg.Name.ShouldBe("root-directory");
        rootArg.Description.ShouldBe("Root directory of git repositories");
    }

    [Fact]
    public async Task ExecuteAsync_NoRepositories_ShouldShowMessage()
    {
        // Arrange
        _mockScanner.Scan(_rootDirectory, Arg.Any<bool>()).Returns([]);

        // Act
        await _command.InvokeAsync([_rootDirectory]);

        // Assert
        _testConsole.Output.ShouldContain("No Git repositories found.");
    }

    [Fact]
    public async Task ExecuteAsync_AllRepositoriesSynced_ShouldShowMessage()
    {
        // Arrange
        var repoPaths = new List<string> { _repoPath };
        _mockScanner.Scan(_rootDirectory, Arg.Any<bool>()).Returns(repoPaths);

        _mockGitService.GetRepositoryStatusAsync(_repoPath, _rootDirectory, Arg.Any<bool>())
            .Returns(new GitRepositoryStatus(REPO_NAME, REPO_NAME, _repoPath, "https://example.com", false, _syncedBranches));

        // Act
        await _command.InvokeAsync([_rootDirectory]);

        // Assert
        _testConsole.Output.ShouldContain("No out-of-sync repositories found.");
    }

    [Fact]
    public async Task ExecuteAsync_ShowOnly_ShouldDisplayOutdatedRepos()
    {
        // Arrange
        var repoPaths = new List<string> { _repoPath };
        _mockScanner.Scan(_rootDirectory, Arg.Any<bool>()).Returns(repoPaths);

        _mockGitService.GetRepositoryStatusAsync(_repoPath, _rootDirectory, Arg.Any<bool>())
            .Returns(new GitRepositoryStatus(REPO_NAME, REPO_NAME, _repoPath, "https://example.com", false, _outdatedBranches));

        _mockDisplayService.When(static x => x.DisplayRepositoriesStatus(Arg.Any<List<GitRepositoryStatus>>(), Arg.Any<string>()))
            .Do(static _ => { });

        // Act
        await _command.InvokeAsync([_rootDirectory, "--show-only"]);

        // Assert
        _mockDisplayService.Received(1).DisplayRepositoriesStatus(Arg.Any<List<GitRepositoryStatus>>(), _rootDirectory);
    }

    [Fact]
    public async Task ExecuteAsync_NoFetch_ShouldNotFetchRepositories()
    {
        // Arrange
        var repoPaths = new List<string> { _repoPath };
        _mockScanner.Scan(_rootDirectory, Arg.Any<bool>()).Returns(repoPaths);

        _mockGitService.GetRepositoryStatusAsync(_repoPath, _rootDirectory, false)
            .Returns(new GitRepositoryStatus(REPO_NAME, REPO_NAME, _repoPath, "https://example.com", false, _outdatedBranches));

        _mockDisplayService.When(static x => x.DisplayRepositoriesStatus(Arg.Any<List<GitRepositoryStatus>>(), Arg.Any<string>()))
            .Do(static _ => { });

        // Act
        await _command.InvokeAsync([_rootDirectory, "--show-only", "--no-fetch"]);

        // Assert
        await _mockGitService.Received(1).GetRepositoryStatusAsync(_repoPath, _rootDirectory, false);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoSubmodulesOption_ShouldNotProcessSubmodules()
    {
        // Arrange
        var repoPaths = new List<string> { _repoPath };
        _mockScanner.Scan(_rootDirectory, false).Returns(repoPaths);

        _mockGitService.GetRepositoryStatusAsync(_repoPath, _rootDirectory, Arg.Any<bool>())
            .Returns(new GitRepositoryStatus(REPO_NAME, REPO_NAME, _repoPath, "https://example.com", false, _syncedBranches));

        // Act
        await _command.InvokeAsync([_rootDirectory, "--no-submodules"]);

        // Assert
        _mockScanner.Received(1).Scan(_rootDirectory, false);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutNoSubmodulesOption_ShouldProcessSubmodules()
    {
        // Arrange
        var repoPaths = new List<string> { _repoPath };
        _mockScanner.Scan(_rootDirectory, true).Returns(repoPaths);

        _mockGitService.GetRepositoryStatusAsync(_repoPath, _rootDirectory, Arg.Any<bool>())
            .Returns(new GitRepositoryStatus(REPO_NAME, REPO_NAME, _repoPath, "https://example.com", false, _syncedBranches));

        // Act
        await _command.InvokeAsync([_rootDirectory]);

        // Assert
        _mockScanner.Received(1).Scan(_rootDirectory, true);
    }

    [Fact]
    public async Task ExecuteAsync_Automatic_ShouldUpdateAllOutdatedRepos()
    {
        // Arrange
        var repoPaths = new List<string> { _repoPath };
        _mockScanner.Scan(_rootDirectory, Arg.Any<bool>()).Returns(repoPaths);

        _mockGitService.GetRepositoryStatusAsync(_repoPath, _rootDirectory, Arg.Any<bool>())
            .Returns(new GitRepositoryStatus(REPO_NAME, REPO_NAME, _repoPath, "https://example.com", false, _outdatedBranches));

        _mockGitService.SynchronizeRepositoryAsync
        (
            Arg.Any<GitRepositoryStatus>(),
            Arg.Any<Action<FormattableString>>(),
            Arg.Any<bool>(),
            Arg.Any<bool>()
        ).Returns(true);

        _mockDisplayService.GetHierarchicalName(_repoPath, _rootDirectory).Returns(REPO_NAME);

        _mockDisplayService.When(static x => x.DisplayRepositoriesStatus(Arg.Any<List<GitRepositoryStatus>>(), Arg.Any<string>()))
            .Do(static _ => { });

        // Act
        await _command.InvokeAsync([_rootDirectory, "--automatic"]);

        // Assert
        _testConsole.Output.ShouldContain("succeeded");
    }

    [Fact]
    public async Task ExecuteAsync_AllSelected_ShouldUpdateAllOutdatedRepos()
    {
        // Arrange
        var repoPaths = new List<string> { _repoPath };
        _mockScanner.Scan(_rootDirectory, Arg.Any<bool>()).Returns(repoPaths);

        _mockGitService.GetRepositoryStatusAsync(_repoPath, _rootDirectory, Arg.Any<bool>())
            .Returns(new GitRepositoryStatus(REPO_NAME, REPO_NAME, _repoPath, "https://example.com", false, _outdatedBranches));

        _mockGitService.WhenForAnyArgs(x => x.SynchronizeRepositoryAsync
        (
            Arg.Any<GitRepositoryStatus>(),
            Arg.Any<Action<FormattableString>>(),
            Arg.Any<bool>(),
            Arg.Any<bool>()
        )).Do(static x => x.Arg<Action<FormattableString>>().Invoke($"test"));

        _mockGitService.SynchronizeRepositoryAsync
        (
            Arg.Any<GitRepositoryStatus>(),
            Arg.Any<Action<FormattableString>>(),
            Arg.Any<bool>(),
            Arg.Any<bool>()
        ).Returns(true);

        _mockDisplayService.GetHierarchicalName(_repoPath, _rootDirectory).Returns(REPO_NAME);

        _mockDisplayService.When(static x => x.DisplayRepositoriesStatus(Arg.Any<List<GitRepositoryStatus>>(), Arg.Any<string>()))
            .Do(static _ => { });

        _testConsole.Input.PushKey(ConsoleKey.Spacebar);
        _testConsole.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _command.InvokeAsync([_rootDirectory]);

        // Assert
        _testConsole.Output.ShouldContain("succeeded");
    }

    [Fact]
    public async Task ExecuteAsync_NoneSelected_ShouldPrintMessage()
    {
        // Arrange
        var repoPaths = new List<string> { _repoPath };
        _mockScanner.Scan(_rootDirectory, Arg.Any<bool>()).Returns(repoPaths);

        _mockGitService.GetRepositoryStatusAsync(_repoPath, _rootDirectory, Arg.Any<bool>())
            .Returns(new GitRepositoryStatus(REPO_NAME, REPO_NAME, _repoPath, "https://example.com", false, _outdatedBranches));

        _mockGitService.SynchronizeRepositoryAsync
        (
            Arg.Any<GitRepositoryStatus>(),
            Arg.Any<Action<FormattableString>>(),
            Arg.Any<bool>(),
            Arg.Any<bool>()
        ).Returns(true);

        _mockDisplayService.GetHierarchicalName(_repoPath, _rootDirectory).Returns(REPO_NAME);

        _mockDisplayService.When(static x => x.DisplayRepositoriesStatus(Arg.Any<List<GitRepositoryStatus>>(), Arg.Any<string>()))
            .Do(static _ => { });

        _testConsole.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _command.InvokeAsync([_rootDirectory]);

        // Assert
        _testConsole.Output.ShouldContain("No repository selected");
    }

    [Fact]
    public async Task ExecuteAsync_ErrorGettingRepositoryStatus_ShouldPrintMessage()
    {
        // Arrange
        var repoPaths = new List<string> { _repoPath };
        _mockScanner.Scan(_rootDirectory, Arg.Any<bool>()).Returns(repoPaths);

        _mockGitService.GetRepositoryStatusAsync(_repoPath, _rootDirectory, Arg.Any<bool>())
            .ThrowsAsync(new InvalidOperationException("Fail to get repository status"));

        // Act
        await _command.InvokeAsync([_rootDirectory]);

        // Assert
        _testConsole.Output.ShouldContain("Fail to get repository status");
    }
}
