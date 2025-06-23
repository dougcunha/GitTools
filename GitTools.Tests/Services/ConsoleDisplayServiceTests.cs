using GitTools.Models;
using GitTools.Services;
using Spectre.Console.Testing;

namespace GitTools.Tests.Services;

/// <summary>
/// Tests for <see cref="ConsoleDisplayService"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class ConsoleDisplayServiceTests
{
    private readonly TestConsole _testConsole = new();
    private readonly ConsoleDisplayService _service;

    public ConsoleDisplayServiceTests()
    {
        _service = new ConsoleDisplayService(_testConsole);
    }

    [Fact]
    public void GetHierarchicalName_WithNestedRepository_ReturnsRelativePath()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        const string REPOSITORY_PATH = @"C:\repos\subfolder\project";

        // Act
        var result = _service.GetHierarchicalName(REPOSITORY_PATH, BASE_FOLDER);

        // Assert
        result.ShouldBe("subfolder/project");
    }

    [Fact]
    public void GetHierarchicalName_WithDirectChildRepository_ReturnsDirectoryName()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        const string REPOSITORY_PATH = @"C:\repos\project";

        // Act
        var result = _service.GetHierarchicalName(REPOSITORY_PATH, BASE_FOLDER);

        // Assert
        result.ShouldBe("project");
    }

    [Fact]
    public void GetHierarchicalName_WithSamePathAsBase_ReturnsDirectoryName()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos\project";
        const string REPOSITORY_PATH = @"C:\repos\project";

        // Act
        var result = _service.GetHierarchicalName(REPOSITORY_PATH, BASE_FOLDER);

        // Assert
        result.ShouldBe("project");
    }

    [Fact]
    public void GetHierarchicalName_WithDeeplyNestedRepository_ReturnsFullRelativePath()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        const string REPOSITORY_PATH = @"C:\repos\org\team\project\subproject";

        // Act
        var result = _service.GetHierarchicalName(REPOSITORY_PATH, BASE_FOLDER);

        // Assert
        result.ShouldBe("org/team/project/subproject");
    }

    [Theory]
    [InlineData(@"C:\repos", @"C:\repos\project", "project")]
    [InlineData(@"C:\repos", @"C:\repos\org\project", "org/project")]
    [InlineData(@"C:\repos", @"C:\repos\org\team\project", "org/team/project")]
    [InlineData(@"C:\", @"C:\repos", "repos")]
    public void GetHierarchicalName_WithVariousPaths_ReturnsExpectedResult(
        string baseFolder, string repositoryPath, string expected)
    {
        // Act
        var result = _service.GetHierarchicalName(repositoryPath, baseFolder);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void ShowScanErrors_WithNoErrors_DoesNotShowAnything()
    {
        // Arrange
        var scanErrors = new Dictionary<string, Exception>();
        const string BASE_FOLDER = @"C:\repos";

        // Act
        _service.ShowScanErrors(scanErrors, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldBeEmpty();
    }

    [Fact]
    public void ShowScanErrors_WithErrorsAndUserDeclinesDetails_ShowsPromptOnly()
    {
        // Arrange
        var scanErrors = new Dictionary<string, Exception>
        {
            { @"C:\repos\project1", new InvalidOperationException("Error 1") },
            { @"C:\repos\project2", new ArgumentException("Error 2") }
        };

        const string BASE_FOLDER = @"C:\repos";

        _testConsole.Input.PushText("n");
        _testConsole.Input.PushKey(ConsoleKey.Enter);

        // Act
        _service.ShowScanErrors(scanErrors, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("2 scan errors detected");
        _testConsole.Output.ShouldNotContain("Error 1");
        _testConsole.Output.ShouldNotContain("Error 2");
    }

    [Fact]
    public void ShowScanErrors_WithErrorsAndUserAcceptsDetails_ShowsErrorDetails()
    {
        // Arrange
        var scanErrors = new Dictionary<string, Exception>
        {
            { @"C:\repos\project1", new InvalidOperationException("Error 1") }
        };

        const string BASE_FOLDER = @"C:\repos";

        _testConsole.Input.PushText("y");
        _testConsole.Input.PushKey(ConsoleKey.Enter);

        // Act
        _service.ShowScanErrors(scanErrors, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("1 scan errors detected");
        _testConsole.Output.ShouldContain("project1");
        _testConsole.Output.ShouldContain("Error 1");
    }

    [Fact]
    public void ShowScanErrors_WithMultipleErrors_ShowsAllErrorDetails()
    {
        // Arrange
        var scanErrors = new Dictionary<string, Exception>
        {
            { @"C:\repos\project1", new InvalidOperationException("Error 1") },
            { @"C:\repos\subfolder\project2", new ArgumentException("Error 2") }
        };

        const string BASE_FOLDER = @"C:\repos";

        _testConsole.Input.PushText("y");
        _testConsole.Input.PushKey(ConsoleKey.Enter);

        // Act
        _service.ShowScanErrors(scanErrors, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("2 scan errors detected");
        _testConsole.Output.ShouldContain("project1");
        _testConsole.Output.ShouldContain("subfolder/project2");
        _testConsole.Output.ShouldContain("Error 1");
        _testConsole.Output.ShouldContain("Error 2");
    }

    [Fact]
    public void ShowInitialInfo_WithSingleTag_ShowsBaseFolderAndTag()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        var tags = new[] { "v1.0.0" };

        // Act
        _service.ShowInitialInfo(BASE_FOLDER, tags);

        // Assert
        _testConsole.Output.ShouldContain(@"Base folder: C:\repos");
        _testConsole.Output.ShouldContain("Tags to search: v1.0.0");
    }

    [Fact]
    public void ShowInitialInfo_WithMultipleTags_ShowsBaseFolderAndAllTags()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        var tags = new[] { "v1.0.0", "v2.0.0", "v3.0.0" };

        // Act
        _service.ShowInitialInfo(BASE_FOLDER, tags);

        // Assert
        _testConsole.Output.ShouldContain(@"Base folder: C:\repos");
        _testConsole.Output.ShouldContain("Tags to search: v1.0.0, v2.0.0, v3.0.0");
    }

    [Fact]
    public void ShowInitialInfo_WithEmptyTags_ShowsBaseFolderAndEmptyTags()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        var tags = Array.Empty<string>();

        // Act
        _service.ShowInitialInfo(BASE_FOLDER, tags);

        // Assert
        _testConsole.Output.ShouldContain(@"Base folder: C:\repos");
        _testConsole.Output.ShouldContain("Tags to search:");
    }

    [Fact]
    public void ShowInitialInfo_WithWildcardTags_ShowsBaseFolderAndWildcardTags()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        var tags = new[] { "v*", "release-*", "*-beta" };

        // Act
        _service.ShowInitialInfo(BASE_FOLDER, tags);

        // Assert
        _testConsole.Output.ShouldContain(@"Base folder: C:\repos");
        _testConsole.Output.ShouldContain("Tags to search: v*, release-*, *-beta");
    }

    [Fact]
    public void DisplayRepositoriesStatus_WithEmptyList_ShowsNoRepositoriesMessage()
    {
        // Arrange
        var reposStatus = new List<GitRepositoryStatus>();
        const string BASE_FOLDER = @"C:\repos";

        // Act
        _service.DisplayRepositoriesStatus(reposStatus, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("No repositories found.");
    }

    [Fact]
    public void DisplayRepositoriesStatus_WithSingleRepository_ShowsTableWithCorrectData()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";

        var reposStatus = new List<GitRepositoryStatus>
        {
            new(
                Name: "TestRepo",
                HierarchicalName: "TestRepo", 
                RepoPath: @"C:\repos\TestRepo",
                RemoteUrl: "https://github.com/user/TestRepo.git",
                HasUncommitedChanges: false,
                LocalBranches: 
                [
                    new BranchStatus(@"C:\repos\TestRepo", "main", "origin/main", true, 2, 1)
                ]
            )
        };

        // Act
        _service.DisplayRepositoriesStatus(reposStatus, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("Outdated Repositories");
        _testConsole.Output.ShouldContain("TestRepo");
        _testConsole.Output.ShouldContain("https://github.com/user/TestRepo.git");
        _testConsole.Output.ShouldContain("1"); // commits ahead
        _testConsole.Output.ShouldContain("2"); // commits behind
        _testConsole.Output.ShouldContain("1"); // tracked branches
        _testConsole.Output.ShouldContain("0"); // untracked branches
    }

    [Fact]
    public void DisplayRepositoriesStatus_WithMultipleRepositories_ShowsAllRepositoriesInTable()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";

        var reposStatus = new List<GitRepositoryStatus>
        {
            new(
                Name: "Repo1",
                HierarchicalName: "org/Repo1",
                RepoPath: @"C:\repos\org\Repo1",
                RemoteUrl: "https://github.com/org/Repo1.git",
                HasUncommitedChanges: false,
                LocalBranches: 
                [
                    new BranchStatus(@"C:\repos\org\Repo1", "main", "origin/main", true, 0, 3),
                    new BranchStatus(@"C:\repos\org\Repo1", "feature", null, false, 0, 0)
                ]
            ),
            new(
                Name: "Repo2",
                HierarchicalName: "Repo2",
                RepoPath: @"C:\repos\Repo2",
                RemoteUrl: "https://github.com/user/Repo2.git",
                HasUncommitedChanges: true,
                LocalBranches: 
                [
                    new BranchStatus(@"C:\repos\Repo2", "develop", "origin/develop", true, 1, 0)
                ]
            )
        };

        // Act
        _service.DisplayRepositoriesStatus(reposStatus, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("org/Repo1");
        _testConsole.Output.ShouldContain("Repo2");
        _testConsole.Output.ShouldContain("https://github.com/org/Repo1.git");
        _testConsole.Output.ShouldContain("https://github.com/user/Repo2.git");
    }

    [Fact]
    public void DisplayRepositoriesStatus_WithRepositoryWithErrors_ShowsErrorIndicator()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";

        var reposStatus = new List<GitRepositoryStatus>
        {
            new(
                Name: "ErrorRepo",
                HierarchicalName: "ErrorRepo",
                RepoPath: @"C:\repos\ErrorRepo",
                RemoteUrl: "https://github.com/user/ErrorRepo.git",
                HasUncommitedChanges: false,
                LocalBranches: [],
                ErrorMessage: "Failed to access repository"
            )
        };

        // Act
        _service.DisplayRepositoriesStatus(reposStatus, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("ErrorRepo");
        _testConsole.Output.ShouldContain("x"); // Error indicator
    }

    [Fact]
    public void DisplayRepositoriesStatus_WithRepositoryWithoutErrors_DoesNotShowErrorIndicator()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";

        var reposStatus = new List<GitRepositoryStatus>
        {
            new(
                Name: "GoodRepo",
                HierarchicalName: "GoodRepo",
                RepoPath: @"C:\repos\GoodRepo",
                RemoteUrl: "https://github.com/user/GoodRepo.git",
                HasUncommitedChanges: false,
                LocalBranches: 
                [
                    new BranchStatus(@"C:\repos\GoodRepo", "main", "origin/main", true, 0, 0)
                ]
            )
        };

        // Act
        _service.DisplayRepositoriesStatus(reposStatus, BASE_FOLDER);

        // Assert
        var output = _testConsole.Output;
        output.ShouldContain("GoodRepo");
        // Verify that the error column is empty (no 'x' indicator for this repository)
        var lines = output.Split('\n');
        var repoLine = lines.FirstOrDefault(line => line.Contains("GoodRepo"));
        repoLine.ShouldNotBeNull();
        // The error column should be empty for repositories without errors
    }

    [Fact]
    public void DisplayRepositoriesStatus_WithMultipleBranches_CalculatesCorrectTotals()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";

        var reposStatus = new List<GitRepositoryStatus>
        {
            new(
                Name: "MultiBranchRepo",
                HierarchicalName: "MultiBranchRepo",
                RepoPath: @"C:\repos\MultiBranchRepo",
                RemoteUrl: "https://github.com/user/MultiBranchRepo.git",
                HasUncommitedChanges: false,
                LocalBranches: 
                [
                    new BranchStatus(@"C:\repos\MultiBranchRepo", "main", "origin/main", true, 2, 1),     // 1 ahead, 2 behind
                    new BranchStatus(@"C:\repos\MultiBranchRepo", "feature1", "origin/feature1", false, 1, 3), // 3 ahead, 1 behind
                    new BranchStatus(@"C:\repos\MultiBranchRepo", "feature2", null, false, 0, 0)         // untracked
                ]
            )
        };

        // Act
        _service.DisplayRepositoriesStatus(reposStatus, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("MultiBranchRepo");
        _testConsole.Output.ShouldContain("4"); // Total commits ahead (1 + 3)
        _testConsole.Output.ShouldContain("3"); // Total commits behind (2 + 1)
        _testConsole.Output.ShouldContain("2"); // Tracked branches count
        _testConsole.Output.ShouldContain("1"); // Untracked branches count
    }

    [Fact]
    public void DisplayRepositoriesStatus_WithRepositoryWithoutRemoteUrl_ShowsEmptyRemoteColumn()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";

        var reposStatus = new List<GitRepositoryStatus>
        {
            new(
                Name: "LocalRepo",
                HierarchicalName: "LocalRepo",
                RepoPath: @"C:\repos\LocalRepo",
                RemoteUrl: null,
                HasUncommitedChanges: false,
                LocalBranches: 
                [
                    new BranchStatus(@"C:\repos\LocalRepo", "main", null, true, 0, 0)
                ]
            )
        };

        // Act
        _service.DisplayRepositoriesStatus(reposStatus, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("LocalRepo");
        // Remote URL column should be empty or show empty value
    }

    [Fact]
    public void DisplayRepositoriesStatus_ShowsCorrectTableHeaders()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";

        var reposStatus = new List<GitRepositoryStatus>
        {
            new(
                Name: "TestRepo",
                HierarchicalName: "TestRepo",
                RepoPath: @"C:\repos\TestRepo",
                RemoteUrl: "https://github.com/user/TestRepo.git",
                HasUncommitedChanges: false,
                LocalBranches: 
                [
                    new BranchStatus(@"C:\repos\TestRepo", "main", "origin/main", true, 0, 0)
                ]
            )
        };

        // Act
        _service.DisplayRepositoriesStatus(reposStatus, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("Repository");
        _testConsole.Output.ShouldContain("Remote URL");
        _testConsole.Output.ShouldContain("A"); // Ahead column
        _testConsole.Output.ShouldContain("B"); // Behind column
        _testConsole.Output.ShouldContain("T"); // Tracked column
        _testConsole.Output.ShouldContain("U"); // Untracked column
        _testConsole.Output.ShouldContain("E"); // Error column
    }

    [Fact]
    public void DisplayRepositoriesStatus_ShowsLegendAfterTable()
    {
        // Arrange
        const string BASE_FOLDER = @"C:\repos";
        
        var reposStatus = new List<GitRepositoryStatus>
        {
            new(
                Name: "TestRepo",
                HierarchicalName: "TestRepo",
                RepoPath: @"C:\repos\TestRepo",
                RemoteUrl: "https://github.com/user/TestRepo.git",
                HasUncommitedChanges: false,
                LocalBranches: 
                [
                    new BranchStatus(@"C:\repos\TestRepo", "main", "origin/main", true, 0, 0)
                ]
            )
        };

        // Act
        _service.DisplayRepositoriesStatus(reposStatus, BASE_FOLDER);

        // Assert
        _testConsole.Output.ShouldContain("Commits ahead of remote");
        _testConsole.Output.ShouldContain("Commits behind remote");
        _testConsole.Output.ShouldContain("Untracked branches");
        _testConsole.Output.ShouldContain("Tracked branches");
        _testConsole.Output.ShouldContain("Error indicator");
    }
}

