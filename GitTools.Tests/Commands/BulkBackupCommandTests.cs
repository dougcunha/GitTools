using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using GitTools.Commands;
using GitTools.Models;
using GitTools.Services;
using Spectre.Console.Testing;

namespace GitTools.Tests.Commands;

[ExcludeFromCodeCoverage]
public sealed class BulkBackupCommandTests
{
    private readonly IGitRepositoryScanner _scanner = Substitute.For<IGitRepositoryScanner>();
    private readonly IGitService _gitService = Substitute.For<IGitService>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly TestConsole _console = new();
    private readonly BulkBackupCommand _command;

    public BulkBackupCommandTests()
    {
        _console.Interactive();
        _command = new BulkBackupCommand(_scanner, _gitService, _fileSystem, _console);
    }

    [Fact]
    public void Constructor_ShouldConfigureArguments()
    {
        _command.Name.ShouldBe("bkp");
        _command.Description.ShouldBe("Generates a JSON file with remote URLs for each repository found.");
        _command.Arguments.Count.ShouldBe(2);
        _command.Arguments[0].Name.ShouldBe("directory");
        _command.Arguments[1].Name.ShouldBe("output");
    }

    [Fact]
    public async Task ExecuteAsync_WithRepositories_ShouldCreateJson()
    {
        // Arrange
        var root = "/repos";
        var repo1 = "/repos/repo1";
        var repo2 = "/repos/repo2";
        _scanner.Scan(root).Returns([repo1, repo2]);

        _gitService.GetGitRepositoryAsync(repo1).Returns(new GitRepository
        {
            Name = "repo1",
            Path = repo1,
            RemoteUrl = "https://example.com/r1.git",
            IsValid = true
        });

        _gitService.GetGitRepositoryAsync(repo2).Returns(new GitRepository
        {
            Name = "repo2",
            Path = repo2,
            RemoteUrl = "https://example.com/r2.git",
            IsValid = true
        });

        var output = "/output/repos.json";

        // Act
        await _command.ExecuteAsync(root, output);

        // Assert
        var json = _fileSystem.File.ReadAllText(output);
        
        var repos = JsonSerializer.Deserialize<List<GitRepository>>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        repos.ShouldNotBeNull();
        repos.Count.ShouldBe(2);
        repos[0].Name.ShouldBe("repo1");
        repos[0].RemoteUrl.ShouldBe("https://example.com/r1.git");
        await _gitService.Received(1).GetGitRepositoryAsync(repo1);
        await _gitService.Received(1).GetGitRepositoryAsync(repo2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoRepositories_ShouldPrintMessageOnly()
    {
        // Arrange
        const string ROOT = "/repos";
        const string OUTPUT = "/output/repos.json";
        _scanner.Scan(ROOT).Returns([]);

        // Act
        await _command.ExecuteAsync(ROOT, OUTPUT);

        // Assert
        _fileSystem.File.Exists(OUTPUT).ShouldBeFalse();
        _console.Output.ShouldContain("No Git repositories found.");
        await _gitService.DidNotReceive().GetGitRepositoryAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIgnoreSubmodules()
    {
        // Arrange
        var root = "/repos";
        var repo1 = "/repos/repo1";
        var submodule = "/repos/sub";
        _scanner.Scan(root).Returns([repo1, submodule]);

        _fileSystem.AddFile(Path.Combine(submodule, ".git"), new MockFileData("gitdir: ../.git/modules/sub"));

        _gitService.GetGitRepositoryAsync(repo1).Returns(new GitRepository
        {
            Name = "repo1",
            Path = repo1,
            RemoteUrl = "https://example.com/r1.git",
            IsValid = true
        });

        var output = "/output/repos.json";

        // Act
        await _command.ExecuteAsync(root, output);

        // Assert
        var repos = JsonSerializer.Deserialize<List<GitRepository>>(_fileSystem.File.ReadAllText(output), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        repos!.Count.ShouldBe(1);
        repos[0].Path.ShouldBe(Path.GetRelativePath(root, repo1));
        await _gitService.Received(1).GetGitRepositoryAsync(repo1);
        await _gitService.DidNotReceive().GetGitRepositoryAsync(submodule);
    }
}

