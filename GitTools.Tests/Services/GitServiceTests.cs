using System.Diagnostics;
using System.IO.Abstractions;
using System.Reflection;
using GitTools.Services;

namespace GitTools.Tests.Services;

[ExcludeFromCodeCoverage]
public sealed class GitServiceTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly GitService _gitService;

    private const string REPO_PATH = @"C:\test\repo";
    private const string TAG_NAME = "v1.0.0";
    private const string GIT_DIR = ".git";

    public GitServiceTests()
        => _gitService = new GitService(_fileSystem, _processRunner);

    private static DataReceivedEventArgs CreateDataReceivedEventArgs(string data)
    {
        var constructor = typeof(DataReceivedEventArgs)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(string)], null);

        return (DataReceivedEventArgs)constructor!.Invoke([data]);
    }

    [Fact]
    public async Task HasTagAsync_WhenTagExists_ShouldReturnTrue()
    {
        // Arrange
        const string GIT_OUTPUT = "v1.0.0";

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.HasTagAsync(REPO_PATH, TAG_NAME);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"tag -l {TAG_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task HasTagAsync_WhenTagDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

                return 0;
            });

        // Act
        var result = await _gitService.HasTagAsync(REPO_PATH, TAG_NAME);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteTagAsync_ShouldCallGitTagDelete()
    {
        // Arrange
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.DeleteTagAsync(REPO_PATH, TAG_NAME);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"tag -d {TAG_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteRemoteTagAsync_ShouldCallGitPushOriginDelete()
    {
        // Arrange
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.DeleteRemoteTagAsync(REPO_PATH, TAG_NAME);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"push origin :refs/tags/{TAG_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task RunGitCommandAsync_WhenGitDirectoryExists_ShouldUseOriginalPath()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        const string EXPECTED_OUTPUT = "On branch main";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);

        _fileSystem.Directory.Exists(gitPath).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(EXPECTED_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        result.ShouldContain(EXPECTED_OUTPUT);

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == GIT_ARGUMENTS &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task RunGitCommandAsync_WhenGitWorktree_ShouldUseMainRepoPath()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        const string MAIN_REPO_PATH = @"C:\main\repo\.git\worktrees\feature";
        const string GIT_WORKTREE_CONTENT = $"gitdir: {MAIN_REPO_PATH}";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);

        _fileSystem.Directory.Exists(gitPath).Returns(false);
        _fileSystem.File.Exists(gitPath).Returns(true);

        #pragma warning disable S6966
        // ReSharper disable once MethodHasAsyncOverload
        _fileSystem.File.ReadAllText(gitPath).Returns(GIT_WORKTREE_CONTENT);
        #pragma warning restore S6966

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.WorkingDirectory == Path.GetFullPath(Path.Combine(REPO_PATH, MAIN_REPO_PATH))),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task RunGitCommandAsync_WhenWorkTreeWithNoPrefix_ShouldReturnRepoPath()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        const string MAIN_REPO_PATH = @"C:\main\repo\.git\worktrees\feature";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);

        _fileSystem.Directory.Exists(gitPath).Returns(false);
        _fileSystem.File.Exists(gitPath).Returns(true);

        #pragma warning disable S6966
        // ReSharper disable once MethodHasAsyncOverload
        _fileSystem.File.ReadAllText(gitPath).Returns(MAIN_REPO_PATH);
        #pragma warning restore S6966

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        var res = await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        res.ShouldBeEmpty(); // No output expected since we are not capturing output in this test

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi => psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task RunGitCommandAsync_WhenGitCommandFails_ShouldThrowException()
    {
        // Arrange
        const string GIT_ARGUMENTS = "invalid-command";
        const string ERROR_MESSAGE = "git: 'invalid-command' is not a git command";

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var errorHandler = callInfo.ArgAt<DataReceivedEventHandler>(2);
                errorHandler?.Invoke(null!, CreateDataReceivedEventArgs(ERROR_MESSAGE));

                return 1;
            });

        // Act & Assert
        var exception = await Should.ThrowAsync<Exception>(
            () => _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS));

        exception.Message.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task RunGitCommandAsync_ShouldConfigureProcessCorrectly()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == GIT_ARGUMENTS &&
                psi.WorkingDirectory == REPO_PATH &&
                psi.RedirectStandardOutput &&
                psi.RedirectStandardError &&
                !psi.UseShellExecute &&
                psi.CreateNoWindow),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetAllTagsAsync_WhenRepositoryHasTags_ShouldReturnAllTags()
    {
        // Arrange
        const string GIT_OUTPUT = """
            v1.0.0
            v1.1.0
            v2.0.0
            release-1.0
            feature-tag
            """;

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.GetAllTagsAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
        result.ShouldContain("v1.0.0");
        result.ShouldContain("v1.1.0");
        result.ShouldContain("v2.0.0");
        result.ShouldContain("release-1.0");
        result.ShouldContain("feature-tag");

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == "tag -l" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetAllTagsAsync_WhenRepositoryHasNoTags_ShouldReturnEmptyList()
    {
        // Arrange
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

                return 0;
            });

        // Act
        var result = await _gitService.GetAllTagsAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAllTagsAsync_WhenRepositoryHasTagsWithWhitespace_ShouldTrimAndFilterEmptyEntries()
    {
        // Arrange
        const string GIT_OUTPUT = """
            v1.0.0  
              v1.1.0
            
            v2.0.0
            
            """;

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.GetAllTagsAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContain("v1.0.0");
        result.ShouldContain("v1.1.0");
        result.ShouldContain("v2.0.0");
    }

    [Fact]
    public async Task GetTagsMatchingPatternAsync_WhenPatternMatchesSomeTags_ShouldReturnMatchingTags()
    {
        // Arrange
        const string GIT_OUTPUT = """
            v1.0.0
            v1.1.0
            v2.0.0
            release-1.0
            feature-tag
            """;

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.GetTagsMatchingPatternAsync(REPO_PATH, "v1.*");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContain("v1.0.0");
        result.ShouldContain("v1.1.0");
        result.ShouldNotContain("v2.0.0");
        result.ShouldNotContain("release-1.0");
        result.ShouldNotContain("feature-tag");
    }

    [Fact]
    public async Task GetTagsMatchingPatternAsync_WhenPatternMatchesNoTags_ShouldReturnEmptyList()
    {
        // Arrange
        const string GIT_OUTPUT = """
            v1.0.0
            v1.1.0
            v2.0.0
            """;

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.GetTagsMatchingPatternAsync(REPO_PATH, "release-*");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("v*", new[] { "v1.0.0", "v1.1.0", "v2.0.0" })]
    [InlineData("*-tag", new[] { "feature-tag" })]
    [InlineData("release-*", new[] { "release-1.0", "release-2.0" })]
    [InlineData("v?.?.?", new[] { "v1.0.0", "v1.1.0", "v2.0.0" })]
    [InlineData("*", new[] { "v1.0.0", "v1.1.0", "v2.0.0", "release-1.0", "release-2.0", "feature-tag" })]
    public async Task GetTagsMatchingPatternAsync_WithVariousPatterns_ShouldReturnCorrectMatches(string pattern, string[] expectedTags)
    {
        // Arrange
        const string GIT_OUTPUT = """
            v1.0.0
            v1.1.0
            v2.0.0
            release-1.0
            release-2.0
            feature-tag
            """;

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.GetTagsMatchingPatternAsync(REPO_PATH, pattern);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(expectedTags.Length);

        foreach (var expectedTag in expectedTags)
        {
            result.ShouldContain(expectedTag);
        }
    }

    [Fact]
    public async Task GetTagsMatchingPatternAsync_WhenPatternIsCaseInsensitive_ShouldMatchRegardlessOfCase()
    {
        // Arrange
        const string GIT_OUTPUT = """
            V1.0.0
            v1.1.0
            Release-1.0
            FEATURE-TAG
            """;

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.GetTagsMatchingPatternAsync(REPO_PATH, "v*");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result.ShouldContain("V1.0.0");
        result.ShouldContain("v1.1.0");
    }

    [Fact]
    public async Task GetTagsMatchingPatternAsync_WhenRepositoryHasNoTags_ShouldReturnEmptyList()
    {
        // Arrange
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

                return 0;
            });

        // Act
        var result = await _gitService.GetTagsMatchingPatternAsync(REPO_PATH, "v*");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("simple", "simple", true)]
    [InlineData("simple", "SIMPLE", true)] // Case insensitive
    [InlineData("simple", "different", false)]
    [InlineData("test*", "test", true)]
    [InlineData("test*", "testing", true)]
    [InlineData("test*", "test123", true)]
    [InlineData("test*", "tes", false)]
    [InlineData("*test", "test", true)]
    [InlineData("*test", "mytest", true)]
    [InlineData("*test", "123test", true)]
    [InlineData("*test", "testing", false)]
    [InlineData("test?", "test1", true)]
    [InlineData("test?", "testa", true)]
    [InlineData("test?", "test", false)]
    [InlineData("test?", "test12", false)]
    [InlineData("v?.?.?", "v1.0.0", true)]
    [InlineData("v?.?.?", "v10.0.0", false)]
    [InlineData("*.*.*", "1.2.3", true)]
    [InlineData("*.*.*", "v1.2.3", true)]
    [InlineData("release-*-final", "release-1.0-final", true)]
    [InlineData("release-*-final", "release-final", false)]
    [InlineData("[test]", "[test]", true)] // Brackets should be escaped
    [InlineData("test.tag", "test.tag", true)] // Dots should be escaped
    [InlineData("test+tag", "test+tag", true)] // Plus should be escaped
    [InlineData("test^tag", "test^tag", true)] // Caret should be escaped
    [InlineData("test$tag", "test$tag", true)] // Dollar should be escaped
    [InlineData("test(tag)", "test(tag)", true)] // Parentheses should be escaped
    [InlineData("test{tag}", "test{tag}", true)] // Braces should be escaped
    [InlineData("test|tag", "test|tag", true)] // Pipe should be escaped
    public async Task ConvertWildcardToRegex_ThroughGetTagsMatchingPatternAsync_ShouldHandleVariousPatterns(
        string pattern, string tagToTest, bool shouldMatch)
    {
        // Arrange
        var gitOutput = tagToTest;

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(gitOutput));

                return 0;
            });

        // Act
        var result = await _gitService.GetTagsMatchingPatternAsync(REPO_PATH, pattern);

        // Assert
        if (shouldMatch)
        {
            result.ShouldContain(tagToTest);
        }
        else
        {
            result.ShouldNotContain(tagToTest);
        }
    }

    [Fact]
    public async Task ConvertWildcardToRegex_ThroughGetTagsMatchingPatternAsync_ShouldEscapeRegexSpecialCharacters()
    {
        // Arrange
        const string SPECIAL_TAG = "v1.0.0+build.123";
        const string PATTERN = "v1.0.0+build.123"; // Should match exactly, not as regex

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(SPECIAL_TAG));

                return 0;
            });

        // Act
        var result = await _gitService.GetTagsMatchingPatternAsync(REPO_PATH, PATTERN);

        // Assert
        result.ShouldContain(SPECIAL_TAG);
    }

    [Fact]
    public async Task ConvertWildcardToRegex_ThroughGetTagsMatchingPatternAsync_ShouldHandleComplexWildcardPatterns()
    {
        // Arrange
        const string GIT_OUTPUT = """
            release-v1.0.0-final
            release-v1.1.0-beta
            release-v2.0.0-final
            feature-branch-test
            hotfix-urgent-fix
            """;

        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.GetTagsMatchingPatternAsync(REPO_PATH, "release-v?.?.?-*");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContain("release-v1.0.0-final");
        result.ShouldContain("release-v1.1.0-beta");
        result.ShouldContain("release-v2.0.0-final");
        result.ShouldNotContain("feature-branch-test");
        result.ShouldNotContain("hotfix-urgent-fix");
    }
}
