using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using GitTools.Commands;
using GitTools.Models;
using GitTools.Services;
using NSubstitute.ExceptionExtensions;
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
    public void Constructor_ShouldConfigureOptions()
    {
        _command.Options.Count.ShouldBe(1);
        _command.Options.ShouldContain(static o => o.Name == "--force-ssh" && o.Description == "Force SSH URLs for cloning repositories");
    }

    [Fact]
    public async Task ExecuteAsync_ConfigFileMissing_ShouldShowError()
    {
        // Arrange & Act
        await _command.Parse("restore missing.json /target --force-ssh").InvokeAsync();

        // Assert
        _console.Output.ShouldContain("Configuration file not found");
        await _gitService.DidNotReceive().RunGitCommandAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_ShouldShowError()
    {
        // Arrange
        _fileSystem.AddFile("config.json", new MockFileData("{invalid"));

        // Act
        await _command.Parse("restore config.json /target").InvokeAsync();

        // Assert
        _console.Output.ShouldContain("Failed to read configuration");
        await _gitService.DidNotReceive().RunGitCommandAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithRepositories_ShouldCloneEach()
    {
        // Arrange
        var repos = new List<GitRepository>
        {
            new() { Name = "repo1", Path = "repo1", RemoteUrl = "https://example.com/r1.git", IsValid = true },
            new() { Name = "repo2", Path = "repo2", RemoteUrl = "https://example.com/r2.git", IsValid = true }
        };

        var json = JsonSerializer.Serialize(repos, GitRepository.JsonSerializerOptions);
        _fileSystem.AddFile("config.json", new MockFileData(json));

        _console.Input.PushKey(ConsoleKey.Spacebar);
        _console.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _command.Parse("restore config.json /work").InvokeAsync();

        // Assert
        await _gitService.Received(1).RunGitCommandAsync("/work", "clone https://example.com/r1.git repo1");
        await _gitService.Received(1).RunGitCommandAsync("/work", "clone https://example.com/r2.git repo2");
    }

    [Fact]
    public async Task ExecuteAsync_WithErrorCloningRepository_ShouldShowError()
    {
        // Arrange
        var repos = new List<GitRepository>
        {
            new() { Name = "repo1", Path = "repo1", RemoteUrl = "https://example.com/r1.git", IsValid = true }
        };

        var json = JsonSerializer.Serialize(repos, GitRepository.JsonSerializerOptions);
        _fileSystem.AddFile("config.json", new MockFileData(json));

        _gitService.RunGitCommandAsync("/work", "clone https://example.com/r1.git repo1")
            .ThrowsAsync(new Exception("Clone failed"));

        _console.Input.PushKey(ConsoleKey.Spacebar);
        _console.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _command.Parse("restore config.json /work").InvokeAsync();

        // Assert
        _console.Output.ShouldContain("Repo1 failed: Clone failed");
    }

    [Fact]
    public async Task ExecuteAsync_WithNoRepositories_ShouldShowNoReposMessage()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new List<GitRepository>(), GitRepository.JsonSerializerOptions);
        _fileSystem.AddFile("config.json", new MockFileData(json));

        // Act
        await _command.Parse("restore config.json /target").InvokeAsync();

        // Assert
        _console.Output.ShouldContain("No repositories found in the configuration");
        await _gitService.DidNotReceive().RunGitCommandAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithRepositoriesButNoneSelected__ShouldShowNoReposMessage()
    {
        // Arrange
        var repos = new List<GitRepository>
        {
            new() { Name = "repo1", Path = "repo1", RemoteUrl = "https://example.com/r1.git", IsValid = true },
            new() { Name = "repo2", Path = "repo2", RemoteUrl = "https://example.com/r2.git", IsValid = true }
        };

        var json = JsonSerializer.Serialize(repos, GitRepository.JsonSerializerOptions);

        _fileSystem.AddFile("config.json", new MockFileData(json));
        _console.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _command.Parse("restore config.json /work").InvokeAsync();

        // Assert
        _console.Output.ShouldContain("No repositories selected for restore");
        await _gitService.DidNotReceive().RunGitCommandAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WithForceSsh_ShouldCloneUsingSshUrls()
    {
        // Arrange
        var repos = new List<GitRepository>
        {
            new() { Name = "repo1", Path = "repo1", RemoteUrl = "https://example.com/r1.git", IsValid = true },
            new() { Name = "repo2", Path = "repo2", RemoteUrl = "git@example:example.com/r1.git", IsValid = true },
            new() { Name = "repo3", Path = "repo3", RemoteUrl = "invalid.com/r1.git", IsValid = true },
        };

        var json = JsonSerializer.Serialize(repos, GitRepository.JsonSerializerOptions);
        _fileSystem.AddFile("config.json", new MockFileData(json));

        _console.Input.PushKey(ConsoleKey.Spacebar);
        _console.Input.PushKey(ConsoleKey.Enter);

        // Act
        await _command.Parse("restore config.json /work --force-ssh").InvokeAsync();

        // Assert
        await _gitService.Received(1).RunGitCommandAsync("/work", "clone git@example.com:r1.git repo1");
    }
}
