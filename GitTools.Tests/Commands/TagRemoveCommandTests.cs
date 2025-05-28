using System.CommandLine;
using GitTools.Commands;
using GitTools.Services;
using NSubstitute.ExceptionExtensions;
using Spectre.Console.Testing;

namespace GitTools.Tests.Commands;

/// <summary>
/// Unit tests for the TagRemoveCommand class.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TagRemoveCommandTests
{
    private readonly IGitRepositoryScanner _mockGitScanner = Substitute.For<IGitRepositoryScanner>();
    private readonly IGitService _mockGitService = Substitute.For<IGitService>();
    private readonly TestConsole _testConsole = new();
    private readonly TagRemoveCommand _command;

    public TagRemoveCommandTests()
    {
        _testConsole.Interactive();
        _command = new TagRemoveCommand(_mockGitScanner, _mockGitService, _testConsole);
    }

    [Fact]
    public void Constructor_ShouldSetCorrectNameAndDescription()
    {
        // Assert
        _command.Name.ShouldBe("rm");
        _command.Description.ShouldBe("Removes tags from git repositories.");
    }

    [Fact]
    public void Constructor_ShouldConfigureArguments()
    {
        // Assert
        _command.Arguments.Count.ShouldBe(2);

        var dirArgument = _command.Arguments[0];
        dirArgument.Name.ShouldBe("directory");
        dirArgument.Description.ShouldBe("Root directory of git repositories");

        var tagsArgument = _command.Arguments[1];
        tagsArgument.Name.ShouldBe("tags");
        tagsArgument.Description.ShouldBe("Tags to remove (comma separated)");
        tagsArgument.Arity.ShouldBe(ArgumentArity.ExactlyOne);
    }

    [Fact]
    public void Constructor_ShouldConfigureRemoteOption()
    {
        // Assert
        _command.Options.Count.ShouldBe(1);

        var remoteOption = _command.Options[0];
        remoteOption.Name.ShouldBe("remote");
        remoteOption.Aliases.ShouldContain("--remote");
        remoteOption.Aliases.ShouldContain("-r");
        remoteOption.Description.ShouldBe("Also remove the tag from the remote repository (if present)");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyTags_ShouldDisplayErrorAndReturn()
    {
        // Act
        await _command.ExecuteAsync("", @"C:\TestRepo", false);

        // Assert
        _testConsole.Output.ShouldContain("No tags specified to remove.");
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceOnlyTags_ShouldDisplayErrorAndReturn()
    {
        // Act
        await _command.ExecuteAsync("   ", @"C:\TestRepo", false);

        // Assert
        _testConsole.Output.ShouldContain("No tags specified to remove.");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoGitRepositories_ShouldDisplayErrorAndReturn()
    {
        // Arrange
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(new List<string>());

        // Act
        await _command.ExecuteAsync("v1.0.0", @"C:\TestRepo", false);

        // Assert
        _testConsole.Output.ShouldContain("No Git repositories found.");
    }

    [Fact]
    public async Task ExecuteAsync_WithRepositoriesFound_ShouldDisplayCount()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1", @"C:\TestRepo\Repo2" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        await _command.ExecuteAsync("v1.0.0", @"C:\TestRepo", false);

        // Assert
        _testConsole.Output.ShouldContain("2 repositories found.");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoRepositoriesHavingTags_ShouldDisplayNotFoundMessage()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1", @"C:\TestRepo\Repo2" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        await _command.ExecuteAsync("v1.0.0", @"C:\TestRepo", false);

        // Assert
        _testConsole.Output.ShouldContain("No repository with the specified tag(s) found.");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleTags_ShouldSplitTagsCorrectly()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0").Returns(true);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v2.0.0").Returns(true);
        _testConsole.Input.PushTextWithEnter(" "); // Select first item
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Confirm selection

        // Act
        await _command.ExecuteAsync("v1.0.0, v2.0.0", @"C:\TestRepo", false);

        // Assert
        await _mockGitService.Received(1).HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0");
        await _mockGitService.Received(1).HasTagAsync(@"C:\TestRepo\Repo1", "v2.0.0");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoRepsitorySelected_ShowMessage()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0").Returns(true);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v2.0.0").Returns(true);
        _testConsole.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _command.ExecuteAsync("v1.0.0, v2.0.0", @"C:\TestRepo", false);

        // Assert
        _testConsole.Output.ShouldContain("No repository selected");
    }

    [Fact]
    public async Task ExecuteAsync_WithTagScanError_ShouldContinueScanning()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1", @"C:\TestRepo\Repo2" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0").Throws(new Exception("Git error"));
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo2", "v1.0.0").Returns(false);
        _testConsole.Input.PushTextWithEnter("n"); // Don't show scan error details

        // Act
        await _command.ExecuteAsync("v1.0.0", @"C:\TestRepo", false);

        // Assert
        await _mockGitService.Received(1).HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0");
        await _mockGitService.Received(1).HasTagAsync(@"C:\TestRepo\Repo2", "v1.0.0");
        _testConsole.Output.ShouldContain("No repository with the specified tag(s) found.");
    }

    [Fact]
    public async Task ExecuteAsync_WithTagScanError_ShouldShowScanError()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1", @"C:\TestRepo\Repo2" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0").Throws(new Exception("Git error"));
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo2", "v1.0.0").Returns(false);
        _testConsole.Input.PushTextWithEnter("y");

        // Act
        await _command.ExecuteAsync("v1.0.0", @"C:\TestRepo", false);

        // Assert
        _testConsole.Output.ShouldContain("Git error");
    }

    [Fact]
    public async Task ExecuteAsync_WithRepositoryHavingTag_ShouldCallDeleteTag()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0").Returns(true);
        _testConsole.Input.PushTextWithEnter(" "); // Select first item
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Confirm selection

        // Act
        await _command.ExecuteAsync("v1.0.0", @"C:\TestRepo", false);

        // Assert
        await _mockGitService.Received(1).DeleteTagAsync(@"C:\TestRepo\Repo1", "v1.0.0");
        await _mockGitService.DidNotReceive().DeleteRemoteTagAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithRemoteOptionTrue_ShouldCallDeleteRemoteTag()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0").Returns(true);
        _testConsole.Input.PushTextWithEnter(" "); // Select first item
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Confirm selection

        // Act
        await _command.ExecuteAsync("v1.0.0", @"C:\TestRepo", true);

        // Assert
        await _mockGitService.Received(1).DeleteTagAsync(@"C:\TestRepo\Repo1", "v1.0.0");
        await _mockGitService.Received(1).DeleteRemoteTagAsync(@"C:\TestRepo\Repo1", "v1.0.0");
    }

    [Fact]
    public async Task ExecuteAsync_WithDeleteTagError_ShouldDisplayErrorMessage()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0").Returns(true);
        _mockGitService.DeleteTagAsync(@"C:\TestRepo\Repo1", "v1.0.0").Throws(new Exception("Delete failed"));
        _testConsole.Input.PushTextWithEnter(" "); // Select first item
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Confirm selection

        // Act
        await _command.ExecuteAsync("v1.0.0", @"C:\TestRepo", false);

        // Assert
        _testConsole.Output.ShouldContain("Delete failed");
        _testConsole.Output.ShouldContain("❌");
    }

    [Fact]
    public async Task ExecuteAsync_WithSuccessfulDelete_ShouldDisplaySuccessMessage()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0").Returns(true);

        // Create a test console that automatically selects the first option
        _testConsole.Input.PushTextWithEnter(" "); // Select first item
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Confirm selection

        // Act
        await _command.ExecuteAsync("v1.0.0", @"C:\TestRepo", false);

        // Assert
        _testConsole.Output.ShouldContain("✅");
        _testConsole.Output.ShouldContain("Done!");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDisplayInitialInfo()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        await _command.ExecuteAsync("v1.0.0,v2.0.0", @"C:\TestRepo", false);

        // Assert
        _testConsole.Output.ShouldContain("Base folder: C:\\TestRepo");
        _testConsole.Output.ShouldContain("Tags to search: v1.0.0, v2.0.0");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleTagsInRepository_ShouldTrackAllFoundTags()
    {
        // Arrange
        var repositories = new List<string> { @"C:\TestRepo\Repo1" };
        _mockGitScanner.Scan(@"C:\TestRepo").Returns(repositories);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v1.0.0").Returns(true);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v2.0.0").Returns(true);
        _mockGitService.HasTagAsync(@"C:\TestRepo\Repo1", "v3.0.0").Returns(false);
        _testConsole.Input.PushTextWithEnter(" "); // Select first item
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Confirm selection

        // Act
        await _command.ExecuteAsync("v1.0.0,v2.0.0,v3.0.0", @"C:\TestRepo", false);

        // Assert
        await _mockGitService.Received(1).DeleteTagAsync(@"C:\TestRepo\Repo1", "v1.0.0");
        await _mockGitService.Received(1).DeleteTagAsync(@"C:\TestRepo\Repo1", "v2.0.0");
        await _mockGitService.DidNotReceive().DeleteTagAsync(@"C:\TestRepo\Repo1", "v3.0.0");
    }
}
