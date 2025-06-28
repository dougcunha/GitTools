using System.CommandLine;
using GitTools.Commands;
using GitTools.Services;
using Spectre.Console.Testing;
using Shouldly;
using NSubstitute;

namespace GitTools.Tests.Commands;

[ExcludeFromCodeCoverage]
public sealed class PruneBranchesCommandTests
{
    private readonly IGitRepositoryScanner _scanner = Substitute.For<IGitRepositoryScanner>();
    private readonly IGitService _gitService = Substitute.For<IGitService>();
    private readonly IConsoleDisplayService _display = Substitute.For<IConsoleDisplayService>();
    private readonly TestConsole _console = new();
    private readonly PruneBranchesCommand _command;

    public PruneBranchesCommandTests()
    {
        _console.Interactive();
        _command = new PruneBranchesCommand(_scanner, _gitService, _console, _display);
    }

    [Fact]
    public void Constructor_ShouldSetNameAndDescription()
    {
        _command.Name.ShouldBe("prune-branches");
        _command.Description.ShouldBe("Prunes local branches in git repositories.");
    }

    [Fact]
    public async Task ExecuteAsync_NoRepositories_ShouldShowMessage()
    {
        // Arrange
        _scanner.Scan("C:/repos").Returns([]);

        // Act
        await _command.Parse(["C:/repos"]).InvokeAsync();

        // Assert
        _console.Output.ShouldContain("No Git repositories found.");
    }

    [Fact]
    public async Task ExecuteAsync_DryRun_ShouldNotDeleteBranches()
    {
        // Arrange
        _scanner.Scan("C:/repos").Returns(["C:/repos/repo1"]);
        _gitService.GetPrunableBranchesAsync("C:/repos/repo1", true, false, null).Returns(["feature"]);
        _display.GetHierarchicalName("C:/repos/repo1", "C:/repos").Returns("repo1");

        // Act
        await _command.Parse(["C:/repos", "--dry-run"]).InvokeAsync();

        // Assert
        await _gitService.DidNotReceive().DeleteLocalBranchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }
}
