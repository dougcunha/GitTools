using System.CommandLine;
using GitTools.Commands;
using GitTools.Services;
using Spectre.Console.Testing;

namespace GitTools.Tests.Commands;

/// <summary>
/// Unit tests for the TagRemoveCommand class.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class TagRemoveCommandTests
{
    private readonly ITagSearchService _mockTagSearchService = Substitute.For<ITagSearchService>();
    private readonly ITagValidationService _mockTagValidationService = Substitute.For<ITagValidationService>();
    private readonly IConsoleDisplayService _mockConsoleDisplayService = Substitute.For<IConsoleDisplayService>();
    private readonly IGitService _mockGitService = Substitute.For<IGitService>();
    private readonly TestConsole _testConsole = new();
    private readonly TagRemoveCommand _command;

    public TagRemoveCommandTests()
    {
        _testConsole.Interactive();
        _command = new TagRemoveCommand(_mockTagSearchService, _mockTagValidationService, _mockConsoleDisplayService, _mockGitService, _testConsole);
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
        var dirArg = _command.Arguments[0];
        dirArg.Name.ShouldBe("directory");
        dirArg.Description.ShouldBe("Root directory of git repositories");
        var tagsArg = _command.Arguments[1];
        tagsArg.Name.ShouldBe("tags");
        tagsArg.Description.ShouldBe("Tags to remove (comma separated)");
        tagsArg.Arity.ShouldBe(ArgumentArity.ExactlyOne);
    }

    [Fact]
    public void Constructor_ShouldConfigureOptions()
    {
        // Assert
        _command.Options.Count.ShouldBe(1);
        var remoteOption = _command.Options[0];
        remoteOption.Name.ShouldBe("--remote");
        remoteOption.Description.ShouldBe("Also remove the tag from the remote repository (if present)");
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyTags_ShouldShowMessage()
    {
        // Arrange
        _mockTagValidationService.ParseAndValidateTags("  ").Returns([]);

        // Act
        await _command.Parse(["rm", "  ", "C:/TestRepo"]).InvokeAsync();

        // Assert
        _testConsole.Output.ShouldContain("No tags specified to remove.");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoRepositories_ShouldShowMessage()
    {
        //Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];
        var searchResult = new TagSearchResult([], [], []);
        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);

        // Act
        await _command.Parse("rm C:/TestRepo v1.0").InvokeAsync();

        // Assert
        _testConsole.Output.ShouldContain("No repository with the specified tag(s) found.");
        _mockConsoleDisplayService.Received(1).ShowScanErrors(searchResult.ScanErrors, "C:/TestRepo");
    }

    [Fact]
    public async Task ExecuteAsync_WithValidTags_ShouldCallServices()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];

        var searchResult = new TagSearchResult
        (
            ["C:/TestRepo/Repo1"],
            new Dictionary<string, List<string>> { ["C:/TestRepo/Repo1"] = ["v1.0"] },
            []
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName("C:/TestRepo/Repo1", "C:/TestRepo").Returns("Repo1");
        _testConsole.Input.PushTextWithEnter(""); // No selection

        // Act
        await _command.Parse(["rm", "C:/TestRepo", TAGS_INPUT]).InvokeAsync();

        // Assert
        _mockTagValidationService.Received(1).ParseAndValidateTags(TAGS_INPUT);
        _mockConsoleDisplayService.Received(1).ShowInitialInfo("C:/TestRepo", tags);
        await _mockTagSearchService.Received(1).SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNoSelectedRepositories_ShouldShowMessage()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];

        var searchResult = new TagSearchResult
        (
            ["C:/TestRepo/Repo1", "C:/TestRepo/Repo2"],
            new Dictionary<string, List<string>>
            {
                ["C:/TestRepo/Repo1"] = ["v1.0"],
                ["C:/TestRepo/Repo2"] = ["v1.0"]
            },
            []
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName("C:/TestRepo/Repo1", "C:/TestRepo").Returns("Repo1");
        _mockConsoleDisplayService.GetHierarchicalName("C:/TestRepo/Repo2", "C:/TestRepo").Returns("Repo2");
        _testConsole.Input.PushTextWithEnter(""); // No selection

        // Act
        await _command.Parse(["rm", "C:/TestRepo", TAGS_INPUT]).InvokeAsync();

        // Assert
        _testConsole.Output.ShouldContain("No repository selected.");
    }

    [Fact]
    public async Task ExecuteAsync_WithSelectedRepositories_ShouldRemoveTagsLocally()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];
        const string REPO_PATH = "C:/TestRepo/Repo1";

        var searchResult = new TagSearchResult
        (
            [REPO_PATH],
            new Dictionary<string, List<string>> { [REPO_PATH] = ["v1.0"] },
            []
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName(REPO_PATH, "C:/TestRepo").Returns("Repo1");
        _testConsole.Input.PushKey(ConsoleKey.Spacebar);
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Select first repository

        // Act
        await _command.Parse(["rm", "C:/TestRepo", TAGS_INPUT]).InvokeAsync();

        // Assert
        await _mockGitService.Received(1).DeleteTagAsync(REPO_PATH, "v1.0");
        await _mockGitService.DidNotReceive().DeleteRemoteTagAsync(Arg.Any<string>(), Arg.Any<string>());
        _testConsole.Output.ShouldContain("Removing tag(s) v1.0...");
        _testConsole.Output.ShouldContain("✅ Repo1");
        _testConsole.Output.ShouldContain("✅ Done!");
    }

    [Fact]
    public async Task ExecuteAsync_WithSelectedRepositoriesAndRemoteFlag_ShouldRemoveTagsLocallyAndRemotely()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];
        const string REPO_PATH = "C:/TestRepo/Repo1";

        var searchResult = new TagSearchResult
        (
            [REPO_PATH],
            new Dictionary<string, List<string>> { [REPO_PATH] = ["v1.0"] },
            []
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName(REPO_PATH, "C:/TestRepo").Returns("Repo1");
        _testConsole.Input.PushKey(ConsoleKey.Spacebar);
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Select first repository

        // Act
        await _command.Parse(["rm", "--remote", "C:/TestRepo", TAGS_INPUT]).InvokeAsync();

        // Assert
        await _mockGitService.Received(1).DeleteTagAsync(REPO_PATH, "v1.0");
        await _mockGitService.Received(1).DeleteRemoteTagAsync(REPO_PATH, "v1.0");
        _testConsole.Output.ShouldContain("Removing tag(s) v1.0...");
        _testConsole.Output.ShouldContain("✅ Repo1");
        _testConsole.Output.ShouldContain("✅ Done!");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleTagsInRepository_ShouldRemoveAllTags()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0,v2.0";
        string[] tags = ["v1.0", "v2.0"];
        const string REPO_PATH = "C:/TestRepo/Repo1";

        var searchResult = new TagSearchResult
        (
            [REPO_PATH],
            new Dictionary<string, List<string>> { [REPO_PATH] = ["v1.0", "v2.0"] },
            []
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName(REPO_PATH, "C:/TestRepo").Returns("Repo1");
        _testConsole.Input.PushKey(ConsoleKey.Spacebar);
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Select first repository

        // Act
        await _command.Parse(["rm", "C:/TestRepo", TAGS_INPUT]).InvokeAsync();

        // Assert
        await _mockGitService.Received(1).DeleteTagAsync(REPO_PATH, "v1.0");
        await _mockGitService.Received(1).DeleteTagAsync(REPO_PATH, "v2.0");
        _testConsole.Output.ShouldContain("Removing tag(s) v1.0, v2.0...");
        _testConsole.Output.ShouldContain("✅ Repo1");
        _testConsole.Output.ShouldContain("✅ Done!");
    }

    [Fact]
    public async Task ExecuteAsync_WhenGitServiceThrowsException_ShouldDisplayErrorAndContinue()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];
        const string REPO_PATH = "C:/TestRepo/Repo1";
        var exception = new InvalidOperationException("Git command failed");

        var searchResult = new TagSearchResult
        (
            [REPO_PATH],
            new Dictionary<string, List<string>> { [REPO_PATH] = ["v1.0"] },
            []
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName(REPO_PATH, "C:/TestRepo").Returns("Repo1");
        _mockGitService.DeleteTagAsync(REPO_PATH, "v1.0").Returns(Task.FromException(exception));
        _testConsole.Input.PushKey(ConsoleKey.Spacebar);
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Select first repository

        // Act
        await _command.Parse(["rm", "C:/TestRepo", TAGS_INPUT]).InvokeAsync();

        // Assert
        await _mockGitService.Received(1).DeleteTagAsync(REPO_PATH, "v1.0");
        _testConsole.Output.ShouldContain("❌ Repo1 (tag: v1.0): Git command failed");
        _testConsole.Output.ShouldContain("✅ Done!");
        _testConsole.Output.ShouldNotContain("✅ Repo1"); // Should not show success for failed repo
    }

    [Fact]
    public async Task ExecuteAsync_WhenRemoteDeleteThrowsException_ShouldDisplayErrorAndContinue()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];
        const string REPO_PATH = "C:/TestRepo/Repo1";
        var exception = new InvalidOperationException("Remote tag deletion failed");

        var searchResult = new TagSearchResult
        (
            [REPO_PATH],
            new Dictionary<string, List<string>> { [REPO_PATH] = ["v1.0"] },
            []
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName(REPO_PATH, "C:/TestRepo").Returns("Repo1");
        _mockGitService.DeleteRemoteTagAsync(REPO_PATH, "v1.0").Returns(Task.FromException(exception));
        _testConsole.Input.PushKey(ConsoleKey.Spacebar);
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Select first repository

        // Act
        await _command.Parse(["rm", "--remote", "C:/TestRepo", TAGS_INPUT]).InvokeAsync();

        // Assert
        await _mockGitService.Received(1).DeleteTagAsync(REPO_PATH, "v1.0");
        await _mockGitService.Received(1).DeleteRemoteTagAsync(REPO_PATH, "v1.0");
        _testConsole.Output.ShouldContain("❌ Repo1 (tag: v1.0): Remote tag deletion failed");
        _testConsole.Output.ShouldContain("✅ Done!");
        _testConsole.Output.ShouldNotContain("✅ Repo1"); // Should not show success for failed repo
    }

    [Fact]
    public async Task ExecuteAsync_WithScanErrors_ShouldShowScanErrors()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];
        const string REPO_PATH = "C:/TestRepo/Repo1";

        var scanErrors = new Dictionary<string, Exception>
        {
            ["C:/TestRepo/ErrorRepo"] = new Exception("Scan error")
        };

        var searchResult = new TagSearchResult
        (
            [REPO_PATH],
            new Dictionary<string, List<string>> { [REPO_PATH] = ["v1.0"] },
            scanErrors
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName(REPO_PATH, "C:/TestRepo").Returns("Repo1");
        _testConsole.Input.PushKey(ConsoleKey.Spacebar);
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Select first repository

        // Act
        await _command.Parse(["rm", "C:/TestRepo", TAGS_INPUT]).InvokeAsync();

        // Assert
        _mockConsoleDisplayService.Received(1).ShowScanErrors(scanErrors, "C:/TestRepo");
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleRepositoriesPartialSelection_ShouldProcessOnlySelected()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];
        const string REPO1_PATH = "C:/TestRepo/Repo1";
        const string REPO2_PATH = "C:/TestRepo/Repo2";

        var searchResult = new TagSearchResult
        (
            [REPO1_PATH, REPO2_PATH],
            new Dictionary<string, List<string>>
            {
                [REPO1_PATH] = ["v1.0"],
                [REPO2_PATH] = ["v1.0"]
            },
            []
        );

        _mockTagSearchService.WhenForAnyArgs
        (
            static substituteCall => substituteCall.SearchRepositoriesWithTagsAsync(
                Arg.Any<string>(),
                Arg.Any<string[]>(),
                Arg.Any<Action<string>?>())
        )
        .Do
        (
            static callInfo =>
            {
                var action = callInfo.Arg<Action<string>?>(); // Capture the action to simulate progress updates
                action?.Invoke("Searching repositories...");
            }
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName(REPO1_PATH, "C:/TestRepo").Returns("Repo1");
        _mockConsoleDisplayService.GetHierarchicalName(REPO2_PATH, "C:/TestRepo").Returns("Repo2");
        _testConsole.Input.PushKey(ConsoleKey.DownArrow); // The first option is SelectAll, so we move down to the first repository
        _testConsole.Input.PushKey(ConsoleKey.Spacebar);
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Select first repository

        // Act
        await _command.Parse(["rm", "C:/TestRepo", TAGS_INPUT]).InvokeAsync();

        // Assert
        await _mockGitService.Received(1).DeleteTagAsync(REPO1_PATH, "v1.0");
        await _mockGitService.DidNotReceive().DeleteTagAsync(REPO2_PATH, Arg.Any<string>());
        _testConsole.Output.ShouldContain("✅ Repo1");
        _testConsole.Output.ShouldNotContain("✅ Repo2");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPartialTagRemovalFails_ShouldShowMixedResults()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0,v2.0";
        string[] tags = ["v1.0", "v2.0"];
        const string REPO_PATH = "C:/TestRepo/Repo1";
        var exception = new InvalidOperationException("Failed to delete v2.0");

        var searchResult = new TagSearchResult
        (
            [REPO_PATH],
            new Dictionary<string, List<string>> { [REPO_PATH] = ["v1.0", "v2.0"] },
            []
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/TestRepo", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName(REPO_PATH, "C:/TestRepo").Returns("Repo1");
        _mockGitService.DeleteTagAsync(REPO_PATH, "v1.0").Returns(Task.CompletedTask);
        _mockGitService.DeleteTagAsync(REPO_PATH, "v2.0").Returns(Task.FromException(exception));
        _testConsole.Input.PushKey(ConsoleKey.Spacebar);
        _testConsole.Input.PushKey(ConsoleKey.Enter); // Select first repository

        // Act
        await _command.Parse(["rm", "C:/TestRepo", TAGS_INPUT]).InvokeAsync();

        // Assert
        await _mockGitService.Received(1).DeleteTagAsync(REPO_PATH, "v1.0");
        await _mockGitService.Received(1).DeleteTagAsync(REPO_PATH, "v2.0");
        _testConsole.Output.ShouldContain("❌ Repo1 (tag: v2.0): Failed to delete v2.0");
        _testConsole.Output.ShouldNotContain("✅ Repo1"); // Should not show success due to partial failure
        _testConsole.Output.ShouldContain("✅ Done!");
    }
}
