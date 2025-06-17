using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using GitTools.Commands;
using GitTools.Models;
using GitTools.Services;
using Spectre.Console.Testing;

namespace GitTools.Tests.Commands;

[ExcludeFromCodeCoverage]
public sealed class BulkRestoreCommandTests
{
    private readonly IGitService _gitService = Substitute.For<IGitService>();
    private readonly MockFileSystem _fileSystem = new();
    private readonly TestConsole _console = new();
    private readonly BulkRestoreCommand _command;

    public BulkRestoreCommandTests()
    {
        _console.Interactive();
        _command = new BulkRestoreCommand(_gitService, _fileSystem, _console);
    }

    [Fact]
    public void Constructor_ShouldConfigureArguments()
    {
        _command.Name.ShouldBe("restore");
        _command.Description.ShouldBe("Clones repositories from a backup configuration file.");
        _command.Arguments.Count.ShouldBe(2);
        _command.Arguments[0].Name.ShouldBe("config-file");
        _command.Arguments[1].Name.ShouldBe("directory");
    }

    [Fact]
    public async Task ExecuteAsync_ConfigFileMissing_ShouldShowError()
    {
        await _command.ExecuteAsync("missing.json", "/target");

        _console.Output.ShouldContain("Configuration file not found");
        await _gitService.DidNotReceive().RunGitCommandAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ShouldShowError()
    {
        _fileSystem.AddFile("config.json", new MockFileData("{invalid"));

        await _command.ExecuteAsync("config.json", "/target");

        _console.Output.ShouldContain("Failed to read configuration");
        await _gitService.DidNotReceive().RunGitCommandAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithRepositories_ShouldCloneEach()
    {
        var repos = new List<GitRepository>
        {
            new() { Name = "repo1", Path = "repo1", RemoteUrl = "https://example.com/r1.git", IsValid = true },
            new() { Name = "repo2", Path = "repo2", RemoteUrl = "https://example.com/r2.git", IsValid = true }
        };

        var json = JsonSerializer.Serialize(repos, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        _fileSystem.AddFile("config.json", new MockFileData(json));

        await _command.ExecuteAsync("config.json", "/work");

        await _gitService.Received(1).RunGitCommandAsync("/work", "clone https://example.com/r1.git repo1");
        await _gitService.Received(1).RunGitCommandAsync("/work", "clone https://example.com/r2.git repo2");
    }
}
