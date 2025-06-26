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
        const string ROOT = "/repos";
        const string REPO1 = "/repos/repo1";
        const string REPO2 = "/repos/repo2";
        _scanner.Scan(ROOT).Returns([REPO1, REPO2]);

        _gitService.GetGitRepositoryAsync(REPO1).Returns(new GitRepository
        {
            Name = "repo1",
            Path = REPO1,
            RemoteUrl = "https://example.com/r1.git",
            IsValid = true
        });

        _gitService.GetGitRepositoryAsync(REPO2).Returns(new GitRepository
        {
            Name = "repo2",
            Path = REPO2,
            RemoteUrl = "https://example.com/r2.git",
            IsValid = true
        });

        const string OUTPUT = "/output/repos.json";

        // Act
        await _command.Parse([ROOT, OUTPUT]).InvokeAsync();

        // Assert
        var json = await _fileSystem.File.ReadAllTextAsync(OUTPUT);

        var repos = JsonSerializer.Deserialize<List<GitRepository>>
        (
            json,
            GitRepository.JsonSerializerOptions
        );

        repos.ShouldNotBeNull();
        repos.Count.ShouldBe(2);
        repos[0].Name.ShouldBe("repo1");
        repos[0].RemoteUrl.ShouldBe("https://example.com/r1.git");
        await _gitService.Received(1).GetGitRepositoryAsync(REPO1);
        await _gitService.Received(1).GetGitRepositoryAsync(REPO2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoRepositories_ShouldPrintMessageOnly()
    {
        // Arrange
        const string ROOT = "/repos";
        const string OUTPUT = "/output/repos.json";
        _scanner.Scan(ROOT).Returns([]);

        // Act
        await _command.Parse([ROOT, OUTPUT]).InvokeAsync();

        // Assert
        _fileSystem.File.Exists(OUTPUT).ShouldBeFalse();
        _console.Output.ShouldContain("No Git repositories found.");
        await _gitService.DidNotReceive().GetGitRepositoryAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIgnoreSubmodules()
    {
        // Arrange
        const string ROOT = "/repos";
        const string REPO1 = "/repos/repo1";
        const string SUBMODULE = "/repos/sub";
        _scanner.Scan(ROOT).Returns([REPO1, SUBMODULE]);

        _fileSystem.AddFile(Path.Combine(SUBMODULE, ".git"), new MockFileData("gitdir: ../.git/modules/sub"));

        _gitService.GetGitRepositoryAsync(REPO1).Returns(new GitRepository
        {
            Name = "repo1",
            Path = REPO1,
            RemoteUrl = "https://example.com/r1.git",
            IsValid = true
        });

        const string OUTPUT = "/output/repos.json";

        // Act
        await _command.Parse([ROOT, OUTPUT]).InvokeAsync();

        // Assert
        var repos = JsonSerializer.Deserialize<List<GitRepository>>
        (
            await _fileSystem.File.ReadAllTextAsync(OUTPUT),
            GitRepository.JsonSerializerOptions
        );

        repos!.Count.ShouldBe(1);
        repos[0].Path.ShouldBe(Path.GetRelativePath(ROOT, REPO1));
        await _gitService.Received(1).GetGitRepositoryAsync(REPO1);
        await _gitService.DidNotReceive().GetGitRepositoryAsync(SUBMODULE);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidRepository_ShouldNotIncludeInBackup()
    {
        // Arrange
        const string ROOT = "/repos";
        const string REPO_VALID = "/repos/valid";
        const string REPO_INVALID = "/repos/invalid";
        const string OUTPUT = "/output/repos.json";
        _scanner.Scan(ROOT).Returns([REPO_VALID, REPO_INVALID]);

        _gitService.GetGitRepositoryAsync(REPO_VALID).Returns(new GitRepository
        {
            Name = "valid",
            Path = REPO_VALID,
            RemoteUrl = "https://example.com/valid.git",
            IsValid = true
        });

        _gitService.GetGitRepositoryAsync(REPO_INVALID).Returns(new GitRepository
        {
            Name = "invalid",
            Path = REPO_INVALID,
            RemoteUrl = "https://example.com/invalid.git",
            IsValid = false
        });

        // Act
        await _command.Parse([ROOT, OUTPUT]).InvokeAsync();

        // Assert
        var repos = JsonSerializer.Deserialize<List<GitRepository>>
        (
            await _fileSystem.File.ReadAllTextAsync(OUTPUT),
            GitRepository.JsonSerializerOptions
        );

        repos.ShouldNotBeNull();
        repos.Count.ShouldBe(1);
        repos[0].Name.ShouldBe("valid");
        repos[0].RemoteUrl.ShouldBe("https://example.com/valid.git");
        await _gitService.Received(1).GetGitRepositoryAsync(REPO_VALID);
        await _gitService.Received(1).GetGitRepositoryAsync(REPO_INVALID);
    }
}
