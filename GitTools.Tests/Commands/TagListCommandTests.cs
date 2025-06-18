using System.CommandLine;
using GitTools.Commands;
using GitTools.Services;
using Spectre.Console.Testing;

namespace GitTools.Tests.Commands;

[ExcludeFromCodeCoverage]
public sealed class TagListCommandTests
{
    private readonly ITagSearchService _mockTagSearchService = Substitute.For<ITagSearchService>();
    private readonly ITagValidationService _mockTagValidationService = Substitute.For<ITagValidationService>();
    private readonly IConsoleDisplayService _mockConsoleDisplayService = Substitute.For<IConsoleDisplayService>();
    private readonly TestConsole _testConsole = new();
    private readonly TagListCommand _command;

    public TagListCommandTests()
    {
        _testConsole.Interactive();
        _command = new TagListCommand(_mockTagSearchService, _mockTagValidationService, _mockConsoleDisplayService, _testConsole);
    }

    [Fact]
    public void Constructor_ShouldSetCorrectNameAndDescription()
    {
        _command.Name.ShouldBe("ls");
        _command.Description.ShouldBe("Lists repositories containing specified tags.");
    }

    [Fact]
    public void Constructor_ShouldConfigureArguments()
    {
        // Act & Assert
        _command.Arguments.Count.ShouldBe(2);

        var dirArg = _command.Arguments[0];
        dirArg.Name.ShouldBe("directory");
        dirArg.Description.ShouldBe("Root directory of git repositories");

        var tagsArg = _command.Arguments[1];
        tagsArg.Name.ShouldBe("tags");
        tagsArg.Description.ShouldBe("Tags to search (comma separated)");
        tagsArg.Arity.ShouldBe(ArgumentArity.ExactlyOne);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyTags_ShouldShowMessage()
    {
        // Arrange
        _mockTagValidationService.ParseAndValidateTags("  ").Returns([]);

        // Act
        await _command.ExecuteAsync("  ", "C:/repos");

        // Assert
        _testConsole.Output.ShouldContain("No tags specified to search.");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoRepositories_ShouldShowMessage()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        var searchResult = new TagSearchResult([], [], []);
        string[] tags = ["v1.0"];
        
        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/repos", tags, Arg.Any<Action<string>>()).Returns(searchResult);

        // Act
        await _command.ExecuteAsync(TAGS_INPUT, "C:/repos");

        // Assert
        _testConsole.Output.ShouldContain("No repository with the specified tag(s) found.");
    }

    [Fact]
    public async Task ExecuteAsync_WithMatchingRepositories_ShouldListThem()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];

        var searchResult = new TagSearchResult
        (
            ["C:/repos/repo1"],
            new Dictionary<string, List<string>> { ["C:/repos/repo1"] = [..tags] },
            []
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/repos", tags, Arg.Any<Action<string>>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName("C:/repos/repo1", "C:/repos").Returns("repo1");

        // Act
        await _command.ExecuteAsync(TAGS_INPUT, "C:/repos");

        // Assert
        _testConsole.Output.ShouldContain("repo1");
        _testConsole.Output.ShouldContain("v1.0");
    }

    [Fact]
    public async Task ExecuteAsync_WithWildcardPatterns_ShouldFilter()
    {
        // Arrange
        const string TAGS_INPUT = "v1.*";
        string[] tags = ["v1.*"];

        var searchResult = new TagSearchResult
        (
            ["C:/repos/repo1"],
            new Dictionary<string, List<string>> { ["C:/repos/repo1"] = ["v1.0"] },
            []
        );

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/repos", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _mockConsoleDisplayService.GetHierarchicalName("C:/repos/repo1", "C:/repos").Returns("repo1");

        // Act
        await _command.ExecuteAsync(TAGS_INPUT, "C:/repos");

        // Assert
        _testConsole.Output.ShouldContain("v1.0");
        _testConsole.Output.ShouldNotContain("v2.0");
    }

    [Fact]
    public async Task ExecuteAsync_WithScanErrors_ShouldPromptAndDisplay()
    {
        // Arrange
        const string TAGS_INPUT = "v1.0";
        string[] tags = ["v1.0"];
        var scanErrors = new Dictionary<string, Exception> { ["C:/repos/repo1"] = new Exception("boom") };
        var searchResult = new TagSearchResult([], [], scanErrors);

        _mockTagValidationService.ParseAndValidateTags(TAGS_INPUT).Returns(tags);
        _mockTagSearchService.SearchRepositoriesWithTagsAsync("C:/repos", tags, Arg.Any<Action<string>?>()).Returns(searchResult);
        _testConsole.Input.PushTextWithEnter("y");

        // Act
        await _command.ExecuteAsync(TAGS_INPUT, "C:/repos");

        // Assert
        _mockConsoleDisplayService.Received(1).ShowScanErrors(scanErrors, "C:/repos");
    }
}
