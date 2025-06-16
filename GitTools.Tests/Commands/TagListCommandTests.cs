using System.CommandLine;
using GitTools.Commands;
using GitTools.Services;
using NSubstitute.ExceptionExtensions;
using Spectre.Console.Testing;

namespace GitTools.Tests.Commands;

[ExcludeFromCodeCoverage]
public sealed class TagListCommandTests
{
    private readonly IGitRepositoryScanner _mockGitScanner = Substitute.For<IGitRepositoryScanner>();
    private readonly IGitService _mockGitService = Substitute.For<IGitService>();
    private readonly TestConsole _testConsole = new();
    private readonly TagListCommand _command;

    public TagListCommandTests()
    {
        _testConsole.Interactive();
        _command = new TagListCommand(_mockGitScanner, _mockGitService, _testConsole);
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
        await _command.ExecuteAsync("  ", "C:/repos");

        _testConsole.Output.ShouldContain("No tags specified to search.");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoRepositories_ShouldShowMessage()
    {
        _mockGitScanner.Scan("C:/repos").Returns([]);

        await _command.ExecuteAsync("v1.0", "C:/repos");

        _testConsole.Output.ShouldContain("No Git repositories found.");
    }

    [Fact]
    public async Task ExecuteAsync_WithMatchingRepositories_ShouldListThem()
    {
        var repos = new List<string> { "C:/repos/repo1" };
        _mockGitScanner.Scan("C:/repos").Returns(repos);
        _mockGitService.GetAllTagsAsync("C:/repos/repo1").Returns(["v1.0", "v2.0"]);

        await _command.ExecuteAsync("v1.0", "C:/repos");

        _testConsole.Output.ShouldContain("repo1");
        _testConsole.Output.ShouldContain("v1.0");
    }

    [Fact]
    public async Task ExecuteAsync_WithWildcardPatterns_ShouldFilter()
    {
        var repos = new List<string> { "C:/repos/repo1" };
        _mockGitScanner.Scan("C:/repos").Returns(repos);
        _mockGitService.GetAllTagsAsync("C:/repos/repo1").Returns(["v1.0", "v2.0", "feature"]);

        await _command.ExecuteAsync("v1.*", "C:/repos");

        _testConsole.Output.ShouldContain("v1.0");
        _testConsole.Output.ShouldNotContain("v2.0");
    }

    [Fact]
    public async Task ExecuteAsync_WithScanErrors_ShouldPromptAndDisplay()
    {
        var repos = new List<string> { "C:/repos/repo1" };
        _mockGitScanner.Scan("C:/repos").Returns(repos);
        _mockGitService.GetAllTagsAsync("C:/repos/repo1").ThrowsAsync(new Exception("boom"));
        _testConsole.Input.PushTextWithEnter("y");

        await _command.ExecuteAsync("v1.0", "C:/repos");

        _testConsole.Output.ShouldContain("boom");
    }
}
