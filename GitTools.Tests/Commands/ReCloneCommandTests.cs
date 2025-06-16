using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using GitTools.Commands;
using GitTools.Models;
using GitTools.Services;
using Spectre.Console.Testing;

namespace GitTools.Tests.Commands;

/// <summary>
/// Unit tests for the ReCloneCommand class.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ReCloneCommandTests
{
    private readonly MockFileSystem _mockFileSystem = new();
    private readonly IBackupService _mockBackupService = Substitute.For<IBackupService>();
    private readonly IGitService _mockGitService = Substitute.For<IGitService>();
    private readonly TestConsole _testConsole = new();
    private readonly ReCloneCommand _command;

    private const string REPO_NAME = "test-repo";
    private const string REPO_PATH = @"C:\current\test-repo";
    private const string PARENT_DIR = @"C:\current";
    private const string REMOTE_URL = "https://github.com/user/test-repo.git";
    private const string BACKUP_FILE = @"C:\current\test-repo-backup.zip";

    public ReCloneCommandTests()
    {
        _testConsole.Interactive();
        _command = new ReCloneCommand(_mockFileSystem, _mockBackupService, _mockGitService, _testConsole);
    }

    [Fact]
    public void Constructor_ShouldSetCorrectNameAndDescription()
    {
        // Assert
        _command.Name.ShouldBe("reclone");
        _command.Description.ShouldBe("Reclones the specified git repository.");
    }

    [Fact]
    public void Constructor_ShouldConfigureArguments()
    {
        // Assert
        _command.Arguments.Count.ShouldBe(1);

        var repositoryNameArgument = _command.Arguments[0];
        repositoryNameArgument.Name.ShouldBe("repository-name");
        repositoryNameArgument.Description.ShouldBe("Git repository folder name relative to the current directory to reclone");
    }

    [Fact]
    public void Constructor_ShouldConfigureOptions()
    {
        // Assert
        _command.Options.Count.ShouldBe(2);

        var noBackupOption = _command.Options.First(o => o.Name == "no-backup");
        noBackupOption.Description.ShouldBe("Do not create a backup zip of the folder");

        var forceOption = _command.Options.First(o => o.Name == "force");
        forceOption.Description.ShouldBe("Ignore uncommitted changes to the repository");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryIsInvalid_ShouldShowErrorAndReturn()
    {
        // Arrange
        var invalidRepo = new GitRepository
        {
            Name = REPO_NAME,
            Path = REPO_PATH,
            IsValid = false
        };

        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(invalidRepo);

        // Act
        await _command.ExecuteAsync(REPO_NAME, false, false);

        // Assert
        _testConsole.Output.ShouldContain($"{REPO_NAME} is not valid or does not exist: {REPO_PATH}");

        await _mockGitService.DidNotReceive().RunGitCommandAsync(Arg.Any<string>(), Arg.Any<string>());
        _mockBackupService.DidNotReceive().CreateBackup(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUncommittedChangesExistAndNotForced_ShouldShowErrorAndReturn()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();

        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);
        _mockGitService.RunGitCommandAsync(REPO_PATH, "status --porcelain").Returns("M modified-file.txt");

        // Act
        await _command.ExecuteAsync(REPO_NAME, false, false);

        // Assert
        _testConsole.Output.ShouldContain("Uncommitted changes detected. Use --force to ignore.");

        await _mockGitService.Received(1).RunGitCommandAsync(REPO_PATH, "status --porcelain");
        _mockBackupService.DidNotReceive().CreateBackup(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenForcedWithUncommittedChanges_ShouldProceedWithReclone()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();
        SetupSuccessfulReclone(validRepo);

        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);

        // Act
        await _command.ExecuteAsync(REPO_NAME, false, true);

        // Assert
        await _mockGitService.DidNotReceive().RunGitCommandAsync(REPO_PATH, "status --porcelain");
        _mockBackupService.Received(1).CreateBackup(Arg.Any<string>(), BACKUP_FILE);
        await _mockGitService.Received(1).RunGitCommandAsync(PARENT_DIR, $"clone {REMOTE_URL} {REPO_NAME}");
        _testConsole.Output.ShouldContain("Repository recloned successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoBackupOption_ShouldSkipBackupCreation()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();
        SetupSuccessfulReclone(validRepo);

        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);
        _mockGitService.RunGitCommandAsync(REPO_PATH, "status --porcelain").Returns(string.Empty);

        // Act
        await _command.ExecuteAsync(REPO_NAME, true, false);

        // Assert
        _mockBackupService.DidNotReceive().CreateBackup(Arg.Any<string>(), Arg.Any<string>());
        await _mockGitService.Received(1).RunGitCommandAsync(PARENT_DIR, $"clone {REMOTE_URL} {REPO_NAME}");
        _testConsole.Output.ShouldContain("Repository recloned successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessfulReclone_ShouldCreateBackupAndClone()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();
        SetupSuccessfulReclone(validRepo);

        _mockFileSystem.AddDirectory(REPO_PATH);
        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);
        _mockGitService.RunGitCommandAsync(REPO_PATH, "status --porcelain").Returns(string.Empty);

        // Act
        await _command.ExecuteAsync(REPO_NAME, false, false);

        // Assert
        _mockBackupService.Received(1).CreateBackup(REPO_PATH, BACKUP_FILE);
        await _mockGitService.Received(1).RunGitCommandAsync(PARENT_DIR, $"clone {REMOTE_URL} {REPO_NAME}");
        await _mockGitService.Received(1).DeleteLocalGitRepositoryAsync(Arg.Is<string>(s => s.StartsWith(REPO_PATH) && s.Contains(REPO_NAME)));

        _testConsole.Output.ShouldContain($"Recloning repository: {REPO_NAME} at {REPO_PATH}");
        _testConsole.Output.ShouldContain($"Backup created: {BACKUP_FILE}");
        _testConsole.Output.ShouldContain("Repository recloned successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDirectoryMoveSucceeds_ShouldRenameAndDelete()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();
        SetupSuccessfulReclone(validRepo);

        _mockFileSystem.AddDirectory(REPO_PATH);
        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);
        _mockGitService.RunGitCommandAsync(REPO_PATH, "status --porcelain").Returns(string.Empty);

        // Act
        await _command.ExecuteAsync(REPO_NAME, false, false);

        // Assert
        var movedDirectories = _mockFileSystem.AllDirectories
            .Where(static d => d.StartsWith(REPO_PATH, StringComparison.Ordinal) && d != REPO_PATH && d.Length > REPO_PATH.Length)
            .ToList();

        movedDirectories.ShouldNotBeEmpty();
        await _mockGitService.Received(1).DeleteLocalGitRepositoryAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDirectoryMoveFails_ShouldStillProceed()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();
        SetupSuccessfulReclone(validRepo);

        // Simulate directory move failure by not adding the directory to mock filesystem
        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);
        _mockGitService.RunGitCommandAsync(REPO_PATH, "status --porcelain").Returns(string.Empty);

        // Act
        await _command.ExecuteAsync(REPO_NAME, false, false);

        // Assert
        await _mockGitService.Received(1).RunGitCommandAsync(PARENT_DIR, $"clone {REMOTE_URL} {REPO_NAME}");
        _testConsole.Output.ShouldContain("Repository recloned successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeleteOldRepositorySucceeds_ShouldShowSuccessMessage()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();

        _mockFileSystem.AddDirectory(REPO_PATH);
        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);
        _mockGitService.RunGitCommandAsync(REPO_PATH, "status --porcelain").Returns(string.Empty);
        _mockGitService.RunGitCommandAsync(PARENT_DIR, $"clone {REMOTE_URL} {REPO_NAME}").Returns("Cloning...");
        _mockGitService.DeleteLocalGitRepositoryAsync(Arg.Any<string>()).Returns(true);

        // Act
        await _command.ExecuteAsync(REPO_NAME, false, false);

        // Assert
        _testConsole.Output.ShouldContain("Old repository deleted:");
        _testConsole.Output.ShouldContain("Repository recloned successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeleteOldRepositoryFails_ShouldStillShowSuccessMessage()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();

        _mockFileSystem.AddDirectory(REPO_PATH);
        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);
        _mockGitService.RunGitCommandAsync(REPO_PATH, "status --porcelain").Returns(string.Empty);
        _mockGitService.RunGitCommandAsync(PARENT_DIR, $"clone {REMOTE_URL} {REPO_NAME}").Returns("Cloning...");
        _mockGitService.DeleteLocalGitRepositoryAsync(Arg.Any<string>()).Returns(false);

        // Act
        await _command.ExecuteAsync(REPO_NAME, false, false);

        // Assert
        _testConsole.Output.ShouldNotContain("Old repository deleted:");
        _testConsole.Output.ShouldContain("Repository recloned successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDirectoryMoveThrowsException_ShouldLogWarningAndProceed()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();
        SetupSuccessfulReclone(validRepo);
        var mockFileSystem = Substitute.For<IFileSystem>();
        var command = new ReCloneCommand(mockFileSystem, _mockBackupService, _mockGitService, _testConsole);

        // Configure mock to throw an exception when Move is called
        mockFileSystem.Directory.Exists(REPO_PATH).Returns(true);

        mockFileSystem.Directory.When(static x => x.Move(Arg.Any<string>(), Arg.Any<string>()))
            .Do(static _ => throw new UnauthorizedAccessException("Access denied"));

        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);
        _mockGitService.RunGitCommandAsync(REPO_PATH, "status --porcelain").Returns(string.Empty);

        // Act
        await command.ExecuteAsync(REPO_NAME, false, false);

        // Assert
        await _mockGitService.Received(1).RunGitCommandAsync(PARENT_DIR, $"clone {REMOTE_URL} {REPO_NAME}");
        await _mockGitService.DidNotReceive().DeleteLocalGitRepositoryAsync(Arg.Any<string>());
        _testConsole.Output.ShouldContain("Attempt to rename folder");
        _testConsole.Output.ShouldContain("failed: Access denied");
        _testConsole.Output.ShouldContain("Repository recloned successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDirectoryMoveThrowsIOException_ShouldLogWarningAndProceed()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();
        SetupSuccessfulReclone(validRepo);
        var mockFileSystem = Substitute.For<IFileSystem>();
        var command = new ReCloneCommand(mockFileSystem, _mockBackupService, _mockGitService, _testConsole);

        // Configure mock to throw IOException when Move is called
        mockFileSystem.Directory.Exists(REPO_PATH).Returns(true);

        mockFileSystem.Directory.When(static x => x.Move(Arg.Any<string>(), Arg.Any<string>()))
            .Do(static _ => throw new IOException("Directory is in use"));

        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);
        _mockGitService.RunGitCommandAsync(REPO_PATH, "status --porcelain").Returns(string.Empty);

        // Act
        await command.ExecuteAsync(REPO_NAME, false, false);

        // Assert
        await _mockGitService.Received(1).RunGitCommandAsync(PARENT_DIR, $"clone {REMOTE_URL} {REPO_NAME}");
        await _mockGitService.DidNotReceive().DeleteLocalGitRepositoryAsync(Arg.Any<string>());
        _testConsole.Output.ShouldContain("Attempt to rename folder");
        _testConsole.Output.ShouldContain("failed: Directory is in use");
        _testConsole.Output.ShouldContain("Repository recloned successfully");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDirectoryNotFound_ShouldProceedWithoutWarning()
    {
        // Arrange
        var validRepo = CreateValidGitRepository();
        SetupSuccessfulReclone(validRepo);
        var mockFileSystem = Substitute.For<IFileSystem>();
        var command = new ReCloneCommand(mockFileSystem, _mockBackupService, _mockGitService, _testConsole);

        // Configure mock to throw DirectoryNotFoundException when Move is called
        mockFileSystem.Directory.Exists(REPO_PATH).Returns(true);

        mockFileSystem.Directory.When(static x => x.Move(Arg.Any<string>(), Arg.Any<string>()))
            .Do(static _ => throw new DirectoryNotFoundException("Directory not found"));

        _mockGitService.GetGitRepositoryAsync(REPO_NAME).Returns(validRepo);
        _mockGitService.RunGitCommandAsync(REPO_PATH, "status --porcelain").Returns(string.Empty);

        // Act
        await command.ExecuteAsync(REPO_NAME, false, false);

        // Assert
        await _mockGitService.Received(1).RunGitCommandAsync(PARENT_DIR, $"clone {REMOTE_URL} {REPO_NAME}");
        await _mockGitService.DidNotReceive().DeleteLocalGitRepositoryAsync(Arg.Any<string>());
        _testConsole.Output.ShouldNotContain("Attempt to rename folder");
        _testConsole.Output.ShouldNotContain("failed:");
        _testConsole.Output.ShouldContain("Repository recloned successfully");
    }

    private GitRepository CreateValidGitRepository()
        => new()
        {
            Name = REPO_NAME,
            Path = REPO_PATH,
            RemoteUrl = REMOTE_URL,
            IsValid = true
        };

    private void SetupSuccessfulReclone(GitRepository repo)
    {
        _mockGitService.RunGitCommandAsync(repo.ParentDir, $"clone {repo.RemoteUrl} {repo.Name}")
            .Returns("Cloning into 'test-repo'...");

        _mockGitService.DeleteLocalGitRepositoryAsync(Arg.Any<string>())
            .Returns(true);
    }
}
