using GitTools.Services;
using Spectre.Console.Testing;

namespace GitTools.Tests.Services;

/// <summary>
/// Tests for <see cref="ConsoleDisplayService"/>.
/// </summary>
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
        const string baseFolder = @"C:\repos";
        const string repositoryPath = @"C:\repos\subfolder\project";

        // Act
        var result = _service.GetHierarchicalName(repositoryPath, baseFolder);

        // Assert
        result.ShouldBe("subfolder/project");
    }

    [Fact]
    public void GetHierarchicalName_WithDirectChildRepository_ReturnsDirectoryName()
    {
        // Arrange
        const string baseFolder = @"C:\repos";
        const string repositoryPath = @"C:\repos\project";

        // Act
        var result = _service.GetHierarchicalName(repositoryPath, baseFolder);

        // Assert
        result.ShouldBe("project");
    }

    [Fact]
    public void GetHierarchicalName_WithSamePathAsBase_ReturnsDirectoryName()
    {
        // Arrange
        const string baseFolder = @"C:\repos\project";
        const string repositoryPath = @"C:\repos\project";

        // Act
        var result = _service.GetHierarchicalName(repositoryPath, baseFolder);

        // Assert
        result.ShouldBe("project");
    }

    [Fact]
    public void GetHierarchicalName_WithDeeplyNestedRepository_ReturnsFullRelativePath()
    {
        // Arrange
        const string baseFolder = @"C:\repos";
        const string repositoryPath = @"C:\repos\org\team\project\subproject";

        // Act
        var result = _service.GetHierarchicalName(repositoryPath, baseFolder);

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
        const string baseFolder = @"C:\repos";

        // Act
        _service.ShowScanErrors(scanErrors, baseFolder);

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

        const string baseFolder = @"C:\repos";

        _testConsole.Input.PushText("n");
        _testConsole.Input.PushKey(ConsoleKey.Enter);

        // Act
        _service.ShowScanErrors(scanErrors, baseFolder);

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

        const string baseFolder = @"C:\repos";

        _testConsole.Input.PushText("y");
        _testConsole.Input.PushKey(ConsoleKey.Enter);

        // Act
        _service.ShowScanErrors(scanErrors, baseFolder);

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
        
        const string baseFolder = @"C:\repos";

        _testConsole.Input.PushText("y");
        _testConsole.Input.PushKey(ConsoleKey.Enter);

        // Act
        _service.ShowScanErrors(scanErrors, baseFolder);

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
        const string baseFolder = @"C:\repos";
        var tags = new[] { "v1.0.0" };

        // Act
        _service.ShowInitialInfo(baseFolder, tags);

        // Assert
        _testConsole.Output.ShouldContain(@"Base folder: C:\repos");
        _testConsole.Output.ShouldContain("Tags to search: v1.0.0");
    }

    [Fact]
    public void ShowInitialInfo_WithMultipleTags_ShowsBaseFolderAndAllTags()
    {
        // Arrange
        const string baseFolder = @"C:\repos";
        var tags = new[] { "v1.0.0", "v2.0.0", "v3.0.0" };

        // Act
        _service.ShowInitialInfo(baseFolder, tags);

        // Assert
        _testConsole.Output.ShouldContain(@"Base folder: C:\repos");
        _testConsole.Output.ShouldContain("Tags to search: v1.0.0, v2.0.0, v3.0.0");
    }

    [Fact]
    public void ShowInitialInfo_WithEmptyTags_ShowsBaseFolderAndEmptyTags()
    {
        // Arrange
        const string baseFolder = @"C:\repos";
        var tags = Array.Empty<string>();

        // Act
        _service.ShowInitialInfo(baseFolder, tags);

        // Assert
        _testConsole.Output.ShouldContain(@"Base folder: C:\repos");
        _testConsole.Output.ShouldContain("Tags to search:");
    }

    [Fact]
    public void ShowInitialInfo_WithWildcardTags_ShowsBaseFolderAndWildcardTags()
    {
        // Arrange
        const string baseFolder = @"C:\repos";
        var tags = new[] { "v*", "release-*", "*-beta" };

        // Act
        _service.ShowInitialInfo(baseFolder, tags);

        // Assert
        _testConsole.Output.ShouldContain(@"Base folder: C:\repos");
        _testConsole.Output.ShouldContain("Tags to search: v*, release-*, *-beta");
    }
}
