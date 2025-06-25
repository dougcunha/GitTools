using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using GitTools.Services;
using GitTools.Models;
using NSubstitute.ExceptionExtensions;
using Spectre.Console.Testing;

namespace GitTools.Tests.Services;

[ExcludeFromCodeCoverage]
public sealed class GitRepositoryScannerTests
{
    private readonly TestConsole _console = new();
    private readonly MockFileSystem _fileSystem;
    private readonly GitRepositoryScanner _scanner;
    private readonly GitToolsOptions _options = new();
    private static readonly string _rootFolder = Path.Combine(Path.GetTempPath(), "repos");
    private const string GIT_DIR = ".git";
    private const string GIT_MODULES_FILE = ".gitmodules";

    public GitRepositoryScannerTests()
    {
        _fileSystem = new MockFileSystem();
        _scanner = new GitRepositoryScanner(_console, _fileSystem, _options);
    }

    [Fact]
    public void Scan_WithSingleGitRepository_ShouldReturnRepository()
    {
        // Arrange
        var repoPath = Path.Combine(_rootFolder, "repo1");
        var gitPath = Path.Combine(repoPath, GIT_DIR);

        _fileSystem.AddDirectory(gitPath);

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(repoPath);
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Scan_WithMultipleGitRepositories_ShouldReturnAllRepositories()
    {
        // Arrange
        var repo1Path = Path.Combine(_rootFolder, "repo1");
        var repo2Path = Path.Combine(_rootFolder, "repo2");
        var git1Path = Path.Combine(repo1Path, GIT_DIR);
        var git2Path = Path.Combine(repo2Path, GIT_DIR);

        _fileSystem.AddDirectory(git1Path);
        _fileSystem.AddDirectory(git2Path);

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(repo1Path);
        result.ShouldContain(repo2Path);
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void Scan_WithRepositoryFilters_ShouldFilterRepositories()
    {
        // Arrange
        _options.RepositoryFilters = ["frontend-*", "*-service"];
        var frontend = Path.Combine(_rootFolder, "frontend-app");
        var backend = Path.Combine(_rootFolder, "backend-api");
        var service = Path.Combine(_rootFolder, "user-service");
        var tools = Path.Combine(_rootFolder, "tools");

        _fileSystem.AddDirectory(Path.Combine(frontend, GIT_DIR));
        _fileSystem.AddDirectory(Path.Combine(backend, GIT_DIR));
        _fileSystem.AddDirectory(Path.Combine(service, GIT_DIR));
        _fileSystem.AddDirectory(Path.Combine(tools, GIT_DIR));

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(frontend);
        result.ShouldContain(service);
        result.ShouldNotContain(backend);
        result.ShouldNotContain(tools);
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void Scan_WithNonMatchingFilter_ShouldReturnEmptyList()
    {
        // Arrange
        _options.RepositoryFilters = ["does-not-match*"];
        var repo = Path.Combine(_rootFolder, "repo");
        _fileSystem.AddDirectory(Path.Combine(repo, GIT_DIR));

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_WithNestedGitRepositories_ShouldReturnParentRepository()
    {
        // Arrange
        var parentRepoPath = Path.Combine(_rootFolder, "parent");
        var nestedRepoPath = Path.Combine(parentRepoPath, "nested");
        var parentGitPath = Path.Combine(parentRepoPath, GIT_DIR);
        var nestedGitPath = Path.Combine(nestedRepoPath, GIT_DIR);

        _fileSystem.AddDirectory(parentGitPath);
        _fileSystem.AddDirectory(nestedGitPath);

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        // The scanner stops exploring subdirectories when it finds a git repository
        // This is correct behavior as nested repositories are usually submodules
        result.ShouldContain(parentRepoPath);
        result.ShouldNotContain(nestedRepoPath);
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Scan_WithGitWorktree_ShouldReturnRepository()
    {
        // Arrange
        var repoPath = Path.Combine(_rootFolder, "worktree");
        var gitFilePath = Path.Combine(repoPath, GIT_DIR);

        _fileSystem.AddFile(gitFilePath, new MockFileData("gitdir: /main/repo/.git/worktrees/feature"));

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(repoPath);
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Scan_WithSubmodules_ShouldReturnMainRepoAndSubmodules()
    {
        // Arrange
        var mainRepoPath = Path.Combine(_rootFolder, "main");
        var submodule1Path = Path.Combine(mainRepoPath, "submodule1");
        var submodule2Path = Path.Combine(mainRepoPath, "submodule2");

        var mainGitPath = Path.Combine(mainRepoPath, GIT_DIR);
        var sub1GitPath = Path.Combine(submodule1Path, GIT_DIR);
        var sub2GitPath = Path.Combine(submodule2Path, GIT_DIR);
        var gitmodulesPath = Path.Combine(mainRepoPath, GIT_MODULES_FILE);

        _fileSystem.AddDirectory(mainGitPath);
        _fileSystem.AddDirectory(sub1GitPath);
        _fileSystem.AddDirectory(sub2GitPath);

        const string GITMODULES_CONTENT = """
            [submodule "submodule1"]
                path = submodule1
                url = https://github.com/user/submodule1.git
            [submodule "submodule2"]
                path = submodule2
                url = https://github.com/user/submodule2.git
            """;

        _fileSystem.AddFile(gitmodulesPath, new MockFileData(GITMODULES_CONTENT));

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(mainRepoPath);
        result.ShouldContain(submodule1Path);
        result.ShouldContain(submodule2Path);
        result.Count.ShouldBe(3);
    }

    [Fact]
    public void Scan_WithIncludeSubmodulesFalse_ShouldNotIncludeSubmodules()
    {
        // Arrange
        var mainRepoPath = Path.Combine(_rootFolder, "main");
        var submodulePath = Path.Combine(mainRepoPath, "submodule");

        var mainGitPath = Path.Combine(mainRepoPath, GIT_DIR);
        var subGitPath = Path.Combine(submodulePath, GIT_DIR);
        var gitmodulesPath = Path.Combine(mainRepoPath, GIT_MODULES_FILE);

        _fileSystem.AddDirectory(mainGitPath);
        _fileSystem.AddDirectory(subGitPath);

        const string GITMODULES_CONTENT = """
            [submodule "submodule"]
                path = submodule
                url = https://github.com/user/submodule.git
            """;

        _fileSystem.AddFile(gitmodulesPath, new MockFileData(GITMODULES_CONTENT));

        // Act
        _options.IncludeSubmodules = false;
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(mainRepoPath);
        result.ShouldNotContain(submodulePath);
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Scan_WithIncludeSubmodulesTrue_ShouldIncludeSubmodules()
    {
        // Arrange
        var mainRepoPath = Path.Combine(_rootFolder, "main");
        var submodulePath = Path.Combine(mainRepoPath, "submodule");

        var mainGitPath = Path.Combine(mainRepoPath, GIT_DIR);
        var subGitPath = Path.Combine(submodulePath, GIT_DIR);
        var gitmodulesPath = Path.Combine(mainRepoPath, GIT_MODULES_FILE);

        _fileSystem.AddDirectory(mainGitPath);
        _fileSystem.AddDirectory(subGitPath);

        const string GITMODULES_CONTENT = """
            [submodule "submodule"]
                path = submodule
                url = https://github.com/user/submodule.git
            """;

        _fileSystem.AddFile(gitmodulesPath, new MockFileData(GITMODULES_CONTENT));

        // Act
        _options.IncludeSubmodules = true;
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(mainRepoPath);
        result.ShouldContain(submodulePath);
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void Scan_WithInvalidSubmodulePath_ShouldIgnoreInvalidSubmodule()
    {
        // Arrange
        var mainRepoPath = Path.Combine(_rootFolder, "main");
        var validSubmodulePath = Path.Combine(mainRepoPath, "valid-submodule");

        var mainGitPath = Path.Combine(mainRepoPath, GIT_DIR);
        var validSubGitPath = Path.Combine(validSubmodulePath, GIT_DIR);
        var gitmodulesPath = Path.Combine(mainRepoPath, GIT_MODULES_FILE);

        _fileSystem.AddDirectory(mainGitPath);
        _fileSystem.AddDirectory(validSubGitPath);

        const string GITMODULES_CONTENT = """
            [submodule "valid-submodule"]
                path = valid-submodule
                url = https://github.com/user/valid-submodule.git
            [submodule "invalid-submodule"]
                path = invalid-submodule
                url = https://github.com/user/invalid-submodule.git
            """;

        _fileSystem.AddFile(gitmodulesPath, new MockFileData(GITMODULES_CONTENT));

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(mainRepoPath);
        result.ShouldContain(validSubmodulePath);
        result.ShouldNotContain(Path.Combine(mainRepoPath, "invalid-submodule"));
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void Scan_WithNonGitDirectories_ShouldNotReturnNonGitDirectories()
    {
        // Arrange
        var nonGitPath = Path.Combine(_rootFolder, "not-a-repo");
        var gitRepoPath = Path.Combine(_rootFolder, "git-repo");
        var gitPath = Path.Combine(gitRepoPath, GIT_DIR);

        _fileSystem.AddDirectory(nonGitPath);
        _fileSystem.AddDirectory(gitPath);

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldNotContain(nonGitPath);
        result.ShouldContain(gitRepoPath);
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void Scan_WithEmptyDirectory_ShouldReturnEmptyList()
    {
        // Arrange
        _fileSystem.AddDirectory(_rootFolder);

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Scan_WithAccessDeniedDirectory_ShouldLogErrorAndContinue()
    {
        // Arrange
        var accessibleRepoPath = Path.Combine(_rootFolder, "accessible");
        var accessibleGitPath = Path.Combine(accessibleRepoPath, GIT_DIR);

        _fileSystem.AddDirectory(accessibleGitPath);

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(accessibleRepoPath);
        // Note: Testing access denied scenarios requires a different approach with MockFileSystem
        // as it doesn't support throwing exceptions on directory access in the same way as Moq
    }

    [Fact]
    public void Scan_WithDuplicateRepositories_ShouldReturnDistinctRepositories()
    {
        // Arrange
        var repoPath = Path.Combine(_rootFolder, "repo");
        var gitPath = Path.Combine(repoPath, GIT_DIR);

        _fileSystem.AddDirectory(gitPath);

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(repoPath);
        result.Count.ShouldBe(1);
        result.Distinct().Count().ShouldBe(result.Count);
    }

    [Fact]
    public void Scan_WithInvalidGitmodulesFile_ShouldLogErrorAndContinue()
    {
        // Arrange
        var mainRepoPath = Path.Combine(_rootFolder, "main");
        var mainGitPath = Path.Combine(mainRepoPath, GIT_DIR);
        var gitmodulesPath = Path.Combine(mainRepoPath, GIT_MODULES_FILE);

        _fileSystem.AddDirectory(mainGitPath);
        _fileSystem.AddFile(gitmodulesPath, new MockFileData("invalid content"));

        // Act
        var result = _scanner.Scan(_rootFolder);

        // Assert
        result.ShouldContain(mainRepoPath);
        // Note: Testing file read exceptions requires a different approach with MockFileSystem
        // as it doesn't support mocking exceptions in the same way as Moq
    }

    [Fact]
    public void Scan_WithExceptionInAddSubmodules_ShouldLogErrorAndContinue()
    {
        // Arrange
        const string GITMODULES_CONTENT = """
            [submodule "valid-submodule"]
                path = valid-submodule
                url = https://github.com/user/valid-submodule.git
            [submodule "invalid-submodule"]
                path = invalid-submodule
                url = https://github.com/user/invalid-submodule.git
            """;

        var mockFileSystem = Substitute.For<IFileSystem>();
        mockFileSystem.File.Exists(Path.Combine(_rootFolder, ".git")).Returns(true);
        mockFileSystem.File.Exists(Path.Combine(_rootFolder, ".gitmodules")).Returns(true);
        mockFileSystem.File.ReadAllText(Arg.Any<string>()).Returns(GITMODULES_CONTENT);

        mockFileSystem.Directory.Exists(Path.Combine(_rootFolder, "valid-submodule", ".git"))
            .Throws(new IOException("Test exception reading submodule directory"));

        var scannerWithException = new GitRepositoryScanner(_console, mockFileSystem, _options);

        // Act
        var result = scannerWithException.Scan(_rootFolder);

        // Assert
        result.Count.ShouldBe(1);

        // Verify error was logged
        _console.Output.ShouldContain("Error processing submodules in");
    }

    [Fact]
    public void Scan_WithExceptionInProcessDirectory_ShouldLogErrorAndContinue()
    {
        // Arrange
        var mockFileSystem = Substitute.For<IFileSystem>();
        mockFileSystem.Directory.GetDirectories(_rootFolder).Throws(new IOException("Test exception accessing directory"));
        var scannerWithException = new GitRepositoryScanner(_console, mockFileSystem, _options);

        // Act
        var result = scannerWithException.Scan(_rootFolder);

        // Assert
        result.ShouldBeEmpty();
        _console.Output.ShouldContain("Test exception accessing");
    }

    [Fact]
    public void ProcessDirectory_AlreadyProcessedPath_ShouldReturnEarly()
    {
        // Arrange
        const string ALREADY_PROCESSED_DIR = @"C:\already-processed";
        
        var console = new TestConsole();
        var fileSystem = Substitute.For<IFileSystem>();
        var scanner = new GitRepositoryScanner(console, fileSystem, new GitToolsOptions());
        
        var gitRepos = new List<string>();
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingDirs = new Stack<string>();
        
        // Pre-add the directory to processedPaths to simulate it was already processed
        processedPaths.Add(ALREADY_PROCESSED_DIR);
        
        // Act
        var processDirectoryMethod = typeof(GitRepositoryScanner)
            .GetMethod("ProcessDirectory", BindingFlags.NonPublic | BindingFlags.Instance);
        
        processDirectoryMethod!.Invoke(
            scanner,
            [ALREADY_PROCESSED_DIR, gitRepos, processedPaths, pendingDirs, true]
        );
        
        // Assert
        gitRepos.ShouldBeEmpty();
        pendingDirs.ShouldBeEmpty();
        fileSystem.Directory.DidNotReceive().Exists(Arg.Any<string>());
        fileSystem.File.DidNotReceive().Exists(Arg.Any<string>());
    }
}

