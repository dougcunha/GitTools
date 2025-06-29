using System.Diagnostics;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using GitTools.Models;
using GitTools.Services;
using GitTools.Tests.Utils;
using Spectre.Console;
using Spectre.Console.Testing;

namespace GitTools.Tests.Services;

[ExcludeFromCodeCoverage]
public sealed class GitServiceTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();
    private readonly TestConsole _console = new();
    private readonly GitToolsOptions _options = new();
    private readonly GitService _gitService;

    private const string REPO_PATH = "C:/test/repo";
    private const string REPO_NAME = "test-repo";
    private const string TAG_NAME = "v1.0.0";
    private const string GIT_DIR = ".git";
    private const string MAIN_BRANCH = "main";
    private const string DEVELOP_BRANCH = "develop";
    private const string REMOTE_URL = "https://github.com/user/repo.git";
    private const string CURRENT_DIRECTORY = "C:/current";

    public GitServiceTests()
        => _gitService = new GitService(_fileSystem, _processRunner, _console, _options);

    private static DataReceivedEventArgs CreateDataReceivedEventArgs(string data)
    {
        var constructor = typeof(DataReceivedEventArgs)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(string)], null);

        return (DataReceivedEventArgs)constructor!.Invoke([data]);
    }

    private void ConfigureGitCommands(GitCommandConfiguratorOptions options)
    {
        var configurator = new GitCommandConfigurator(options);

        _processRunner.RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                return configurator.ProcessCommand(psi.Arguments, outputHandler);
            });
    }

    private void ConfigureRepositoryStatus
    (
        string remoteUrl,
        List<string> localBranches,
        List<string>? modifiedFiles = null,
        Dictionary<string, string>? upstreamBranches = null,
        Dictionary<string, (int ahead, int behind)>? aheadBehindCounts = null,
        Dictionary<string, bool>? goneBranches = null,
        Dictionary<string, DateTime>? lastCommitDates = null,
        string? currentBranch = null)
    {
        var options = new GitCommandConfiguratorOptions
        {
            RemoteUrl = remoteUrl,
            LocalBranches = localBranches,
            ModifiedFiles = modifiedFiles,
            CurrentBranch = currentBranch,
            UpstreamBranches = upstreamBranches,
            AheadBehindCounts = aheadBehindCounts,
            GoneBranches = goneBranches,
            LastCommitDates = lastCommitDates
        };

        ConfigureGitCommands(options);
    }

    private void ConfigureBranchStatus
    (
        List<string> localBranches,
        string? currentBranch = null,
        Dictionary<string, string>? upstreamBranches = null,
        Dictionary<string, (int ahead, int behind)>? aheadBehindCounts = null,
        Dictionary<string, bool>? goneBranches = null,
        Dictionary<string, DateTime>? lastCommitDates = null,
        List<string>? mergedBranches = null)
    {
        var options = new GitCommandConfiguratorOptions
        {
            LocalBranches = localBranches,
            CurrentBranch = currentBranch,
            UpstreamBranches = upstreamBranches,
            AheadBehindCounts = aheadBehindCounts,
            GoneBranches = goneBranches,
            LastCommitDates = lastCommitDates,
            MergedBranches = mergedBranches
        };

        ConfigureGitCommands(options);
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
    public async Task RunGitCommandAsync_WithLogGitCommandsOption_ShouldWriteGitCommandsToConsole()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);
        _fileSystem.Directory.Exists(gitPath).Returns(true);
        _options.LogAllGitCommands = true;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        _ = await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        _console.Output.ShouldContain($"{REPO_PATH}> git {GIT_ARGUMENTS}");
    }

    [Fact]
    public async Task RunGitCommandAsync_WithNoLogGitCommandsOption_ShouldNotWriteGitCommandsToConsole()
    {
        // Arrange
        const string GIT_ARGUMENTS = "status";
        var gitPath = Path.Combine(REPO_PATH, GIT_DIR);
        _fileSystem.Directory.Exists(gitPath).Returns(true);
        _options.LogAllGitCommands = false;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        _ = await _gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        _console.Output.ShouldNotContain($"{REPO_PATH}> git {GIT_ARGUMENTS}");
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

        // ReSharper disable once MethodHasAsyncOverload
        _fileSystem.File.ReadAllText(gitPath).Returns(GIT_WORKTREE_CONTENT);

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

        // ReSharper disable once MethodHasAsyncOverload
        _fileSystem.File.ReadAllText(gitPath).Returns(MAIN_REPO_PATH);

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
        var fileSystem = new MockFileSystem();
        var processRunner = Substitute.For<IProcessRunner>();
        var console = Substitute.For<IAnsiConsole>();
        var gitService = new GitService(fileSystem, processRunner, console, new GitToolsOptions());
        fileSystem.Directory.CreateDirectory(REPO_PATH);
        fileSystem.Directory.CreateDirectory(Path.Combine(REPO_PATH, GIT_DIR));

        processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await gitService.RunGitCommandAsync(REPO_PATH, GIT_ARGUMENTS);

        // Assert
        await processRunner.Received(1).RunAsync
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
            .Returns(static callInfo =>
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
            Arg.Is<ProcessStartInfo>(static psi =>
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
            .Returns(static callInfo =>
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
            .Returns(static callInfo =>
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
            .Returns(static callInfo =>
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
            .Returns(static callInfo =>
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
            .Returns(static callInfo =>
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
            .Returns(static callInfo =>
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
            .Returns(static callInfo =>
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
            .Returns(static callInfo =>
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
            .Returns(static callInfo =>
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

    [Fact]
    public async Task GetGitRepositoryAsync_WhenValidRepository_ShouldReturnValidGitRepository()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var processRunner = Substitute.For<IProcessRunner>();
        var console = Substitute.For<IAnsiConsole>();
        var gitService = new GitService(fileSystem, processRunner, console, new GitToolsOptions());

        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);

        fileSystem.Directory.SetCurrentDirectory(CURRENT_DIRECTORY);
        fileSystem.Directory.CreateDirectory(REPO_NAME);
        fileSystem.Directory.CreateDirectory(gitPath);

        processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(REMOTE_URL));

                return Task.FromResult(0);
            });

        // Act
        var result = await gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBe(REMOTE_URL);
        result.IsValid.ShouldBeTrue();

        await processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == "config --get remote.origin.url" &&
                psi.WorkingDirectory == REPO_NAME),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenRepositoryNotExists_ShouldReturnInvalidGitRepository()
    {
        // Arrange

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(false);

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBeNull();
        result.IsValid.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>());
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenGitDirectoryNotExists_ShouldReturnInvalidGitRepository()
    {
        // Arrange
        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(true);
        _fileSystem.Directory.Exists(gitPath).Returns(false);
        _fileSystem.File.Exists(gitPath).Returns(false);

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBeNull();
        result.IsValid.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>());
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenGitCommandFails_ShouldReturnInvalidGitRepository()
    {
        // Arrange
        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(true);
        _fileSystem.Directory.Exists(gitPath).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(callInfo =>
            {
                var errorHandler = callInfo.ArgAt<DataReceivedEventHandler>(2);
                errorHandler?.Invoke(null!, CreateDataReceivedEventArgs("fatal: not a git repository"));

                return Task.FromResult(1);
            });

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBeNull();
        result.IsValid.ShouldBeFalse();
    }

    private static async IAsyncEnumerable<string> MockReadLinesAsync(string content)
    {
        foreach (var line in content.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            await Task.Yield();

            yield return line;
        }
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenGitCommandFailsButConfigFileExists_ShouldReturnRepositoryWithUrlFromConfig()
    {
        // Arrange
        const string CONFIG_CONTENT = """
            [core]
                repositoryformatversion = 0
                filemode = true
                bare = false
            [remote "origin"]
                url = https://github.com/user/config-repo.git
                fetch = +refs/heads/*:refs/remotes/origin/*
            """;

        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);
        var configPath = Path.Combine(REPO_NAME, GIT_DIR, "config");

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(true);
        _fileSystem.Directory.Exists(gitPath).Returns(true);
        _fileSystem.File.Exists(configPath).Returns(true);

        _fileSystem.File.ReadLinesAsync(configPath).Returns(MockReadLinesAsync(CONFIG_CONTENT));

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var errorHandler = callInfo.ArgAt<DataReceivedEventHandler>(2);
                errorHandler?.Invoke(null!, CreateDataReceivedEventArgs("fatal: unable to read config"));

                return Task.FromResult(1);
            });

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBe("https://github.com/user/config-repo.git");
        result.IsValid.ShouldBeTrue();
        result.HasErrors.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == "config --get remote.origin.url" &&
                psi.WorkingDirectory == REPO_NAME),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        _fileSystem.File.Received(1).ReadLinesAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenGitCommandFailsButConfigFileExistsWithNoRemote_ShouldReturnRepositoryWithoutUrl()
    {
        // Arrange
        const string CONFIG_CONTENT = """
        [core]
            repositoryformatversion = 0
            filemode = true
            bare = false
        [remote "origin"]
            fetch = +refs/heads/*:refs/remotes/origin/*
        """;

        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);
        var configPath = Path.Combine(REPO_NAME, GIT_DIR, "config");

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(true);
        _fileSystem.Directory.Exists(gitPath).Returns(true);
        _fileSystem.File.Exists(configPath).Returns(true);

        _fileSystem.File.ReadLinesAsync(configPath).Returns(MockReadLinesAsync(CONFIG_CONTENT));

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var errorHandler = callInfo.ArgAt<DataReceivedEventHandler>(2);
                errorHandler?.Invoke(null!, CreateDataReceivedEventArgs("fatal: unable to read config"));

                return Task.FromResult(1);
            });

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBeNull();
        result.IsValid.ShouldBeFalse();
        result.HasErrors.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == "config --get remote.origin.url" &&
                psi.WorkingDirectory == REPO_NAME),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        _fileSystem.File.Received(1).ReadLinesAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task GetGitRepositoryAsync_WhenGitCommandFailsAndConfigFileNotExists_ShouldReturnRepositoryWithoutUrl()
    {
        // Arrange
        var gitPath = Path.Combine(REPO_NAME, GIT_DIR);
        var configPath = Path.Combine(REPO_NAME, GIT_DIR, "config");

        _fileSystem.Directory.GetCurrentDirectory().Returns(CURRENT_DIRECTORY);
        _fileSystem.Directory.Exists(REPO_NAME).Returns(true);
        _fileSystem.Directory.Exists(gitPath).Returns(true);
        _fileSystem.File.Exists(configPath).Returns(false);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var errorHandler = callInfo.ArgAt<DataReceivedEventHandler>(2);
                errorHandler?.Invoke(null!, CreateDataReceivedEventArgs("fatal: unable to read config"));

                return Task.FromResult(1);
            });

        // Act
        var result = await _gitService.GetGitRepositoryAsync(REPO_NAME);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe(REPO_NAME);
        result.Path.ShouldBe(REPO_NAME);
        result.RemoteUrl.ShouldBeNull();
        result.IsValid.ShouldBeFalse();
        result.HasErrors.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>
            (
                static psi =>
                    psi.FileName == "git" &&
                    psi.Arguments == "config --get remote.origin.url" &&
                    psi.WorkingDirectory == REPO_NAME),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        _fileSystem.File.DidNotReceive().ReadLinesAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_ShouldUseAppropriatedCommand()
    {
        // Arrange
        const string REPOSITORY_PATH = "C:/test/repo";
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        _fileSystem.Directory.Exists(REPOSITORY_PATH).Returns(false);

        // Act
        var result = await _gitService.DeleteLocalGitRepositoryAsync(REPOSITORY_PATH);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>
            (
                psi =>
                    psi.FileName.Contains(isWindows ? "powershell" : "bash") &&
                    psi.Arguments.Contains(isWindows ? $"Remove-Item -Recurse -Force '{REPOSITORY_PATH}'" : "rm -rf")
            ),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_WhenPathIsNull_ShouldReturnFalse()
    {
        // Act
        var result = await _gitService.DeleteLocalGitRepositoryAsync(null);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>());
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_WhenPathIsEmpty_ShouldReturnFalse()
    {
        // Act
        var result = await _gitService.DeleteLocalGitRepositoryAsync(string.Empty);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>());
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_WhenCommandFails_ShouldReturnFalse()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\test\repo";
        var fileSystem = new MockFileSystem();
        var processRunner = Substitute.For<IProcessRunner>();
        var console = new TestConsole();
        var gitService = new GitService(fileSystem, processRunner, console, new GitToolsOptions());
        fileSystem.Directory.CreateDirectory(REPOSITORY_PATH);

        processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(1);

        // Act
        var result = await gitService.DeleteLocalGitRepositoryAsync(REPOSITORY_PATH);

        // Assert
        result.ShouldBeFalse();

        console.Output.ShouldContain($"Error deleting repository {REPOSITORY_PATH}: result code 1");
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_WhenExceptionOccurs_ShouldReturnFalse()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\test\repo";
        const string ERROR_MESSAGE = "Access denied";
        var fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory(REPOSITORY_PATH);
        var processRunner = Substitute.For<IProcessRunner>();
        var console = new TestConsole();
        var gitService = new GitService(fileSystem, processRunner, console, new GitToolsOptions());

        processRunner.When(static x => x.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>()))
            .Do(static _ => throw new Exception(ERROR_MESSAGE));

        // Act
        var result = await gitService.DeleteLocalGitRepositoryAsync(REPOSITORY_PATH);

        // Assert
        result.ShouldBeFalse();

        console.Output.ShouldContain($"Error deleting repository {REPOSITORY_PATH}: {ERROR_MESSAGE}");
    }

    [Fact]
    public async Task DeleteLocalGitRepositoryAsync_WhenDirectoryStillExists_ShouldReturnFalse()
    {
        // Arrange
        const string REPOSITORY_PATH = @"C:\test\repo";

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        _fileSystem.Directory.Exists(REPOSITORY_PATH).Returns(true);

        // Act
        var result = await _gitService.DeleteLocalGitRepositoryAsync(REPOSITORY_PATH);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WithUncommittedChanges_ShouldReturnTrue()
    {
        // Arrange
        const string GIT_OUTPUT = "M modified_file.txt\nA new_file.txt";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.HasUncommittedChangesAsync(REPO_PATH);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "status --porcelain" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WithNoChanges_ShouldReturnFalse()
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

                return 0;
            });

        // Act
        var result = await _gitService.HasUncommittedChangesAsync(REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "status --porcelain" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git command failed";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.HasUncommittedChangesAsync(REPO_PATH);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error checking uncommitted changes");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task HasUncommittedChangesAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.HasUncommittedChangesAsync(NON_EXISTENT_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WithAheadAndBehindCommits_ShouldReturnCorrectCounts()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string GIT_FETCH_OUTPUT = "";
        const string GIT_COUNT_OUTPUT = "2\t3";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                if (psi.Arguments.Contains("fetch"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_FETCH_OUTPUT));
                }
                else if (psi.Arguments.Contains("rev-list"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_COUNT_OUTPUT));
                }

                return 0;
            });

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ahead.ShouldBe(2);
        result.behind.ShouldBe(3);

        await _processRunner.Received(2).RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WithNoCommitDifferences_ShouldReturnZeros()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string GIT_FETCH_OUTPUT = "";
        const string GIT_COUNT_OUTPUT = "0\t0";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                if (psi.Arguments.Contains("fetch"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_FETCH_OUTPUT));
                }
                else if (psi.Arguments.Contains("rev-list"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_COUNT_OUTPUT));
                }

                return 0;
            });

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ahead.ShouldBe(0);
        result.behind.ShouldBe(0);
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WithFetchDisabled_ShouldSkipFetchCommand()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string GIT_COUNT_OUTPUT = "1\t0";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_COUNT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(REPO_PATH, BRANCH_NAME, fetch: false);

        // Assert
        result.ahead.ShouldBe(1);
        result.behind.ShouldBe(0);

        await _processRunner.Received(1).RunAsync(
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments.Contains("rev-list") &&
                !psi.Arguments.Contains("fetch")),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WhenRepositoryDoesNotExist_ShouldReturnZeros()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(NON_EXISTENT_REPO_PATH, BRANCH_NAME);

        // Assert
        result.ahead.ShouldBe(0);
        result.behind.ShouldBe(0);

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WhenGitCommandFails_ShouldReturnZerosAndLogError()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string ERROR_MESSAGE = "Git command failed";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ahead.ShouldBe(0);
        result.behind.ShouldBe(0);
        _console.Output.ShouldContain("Error getting remote ahead/behind count");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task GetRemoteAheadBehindCountAsync_WithInvalidParseData_ShouldReturnZeros()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string GIT_FETCH_OUTPUT = "";
        const string INVALID_GIT_COUNT_OUTPUT = "invalid\tdata";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                if (psi.Arguments.Contains("fetch"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_FETCH_OUTPUT));
                }
                else if (psi.Arguments.Contains("rev-list"))
                {
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(INVALID_GIT_COUNT_OUTPUT));
                }

                return 0;
            });

        // Act
        var result = await _gitService.GetRemoteAheadBehindCountAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ahead.ShouldBe(0);
        result.behind.ShouldBe(0);
    }

    [Fact]
    public async Task FetchAsync_WithoutPrune_ShouldExecuteBasicFetchCommand()
    {
        // Arrange
        const string GIT_FETCH_OUTPUT = "";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_FETCH_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.FetchAsync(REPO_PATH);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "fetch --all --tags" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task FetchAsync_WithPrune_ShouldExecuteFetchCommandWithPruneFlag()
    {
        // Arrange
        const string GIT_FETCH_OUTPUT = "";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_FETCH_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.FetchAsync(REPO_PATH, prune: true);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "fetch --all --tags --prune" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task FetchAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.FetchAsync(NON_EXISTENT_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task FetchAsync_WithEmptyRepositoryPath_ShouldReturnFalse()
    {
        // Arrange
        const string EMPTY_REPO_PATH = "";

        // Act
        var result = await _gitService.FetchAsync(EMPTY_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task FetchAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git fetch failed";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.FetchAsync(REPO_PATH);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error fetching updates");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WithUntrackedBranchAndPushNewBranchesFalse_ShouldReturnTrueWithoutExecutingCommands()
    {
        // Arrange
        const string BRANCH_NAME = "feature-branch";
        var untrackedBranch = new BranchStatus(REPO_PATH, BRANCH_NAME, null, false, 0, 0, false, false, DateTime.Now, true);

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        // Act
        var result = await _gitService.SynchronizeBranchAsync(untrackedBranch, pushNewBranches: false);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WithUntrackedBranchAndPushNewBranchesTrue_ShouldExecutePushUpstreamCommand()
    {
        // Arrange
        const string BRANCH_NAME = "feature-branch";
        const string GIT_PUSH_OUTPUT = "";
        var untrackedBranch = new BranchStatus(REPO_PATH, BRANCH_NAME, null, false, 0, 0, false, false, DateTime.Now, true);

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_PUSH_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.SynchronizeBranchAsync(untrackedBranch, pushNewBranches: true);

        // Assert
        result.ShouldBeTrue();
        _console.Output.ShouldContain($"Pushing new branch {BRANCH_NAME} to remote");

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"push --set-upstream origin {BRANCH_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WithTrackedBranch_ShouldExecuteFullSynchronizationSequence()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string UPSTREAM = "origin/main";
        const string GIT_OUTPUT = "";
        var trackedBranch = new BranchStatus(REPO_PATH, BRANCH_NAME, UPSTREAM, true, 1, 2, false, false, DateTime.Now, true);

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.SynchronizeBranchAsync(trackedBranch);

        // Assert
        result.ShouldBeTrue();

        // Verify all three commands were executed in sequence
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.Arguments == $"branch --quiet --set-upstream-to=origin/{BRANCH_NAME} {BRANCH_NAME}"),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.Arguments == $"checkout {BRANCH_NAME}"),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.Arguments == $"rebase --autostash origin/{BRANCH_NAME}"),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        var branch = new BranchStatus(NON_EXISTENT_REPO_PATH, BRANCH_NAME, "origin/main", true, 0, 0, false, false, DateTime.Now, true);

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.SynchronizeBranchAsync(branch);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WithEmptyRepositoryPath_ShouldReturnFalse()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string EMPTY_REPO_PATH = "";
        var branch = new BranchStatus(EMPTY_REPO_PATH, BRANCH_NAME, "origin/main", true, 0, 0, false, false, DateTime.Now, true);

        // Act
        var result = await _gitService.SynchronizeBranchAsync(branch);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeBranchAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string UPSTREAM = "origin/main";
        const string ERROR_MESSAGE = "Git synchronization failed";
        var trackedBranch = new BranchStatus(REPO_PATH, BRANCH_NAME, UPSTREAM, true, 0, 0, false, false, DateTime.Now, true);

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.SynchronizeBranchAsync(trackedBranch);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain($"Error synchronizing branch {BRANCH_NAME}");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenSuccessful_ShouldExecuteExpectedCommands()
    {
        // Arrange
        var branch = new BranchStatus(REPO_PATH, "main", "origin/main", true, 0, 0, false, false, DateTime.Now, true);
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, false, [branch]);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var checkoutMainCalled = false;
        var rebaseAutoStashCalled = false;
        var setUpstreamCalled = false;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        _processRunner.WhenForAnyArgs
        (
            ctx => ctx.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
        ).Do
        (
            info =>
            {
                var startInfo = info.Arg<ProcessStartInfo>();

                if (startInfo.Arguments.StartsWith("checkout main", StringComparison.Ordinal))
                    checkoutMainCalled = true;
                else if (startInfo.Arguments.StartsWith("rebase --autostash origin/main", StringComparison.Ordinal))
                    rebaseAutoStashCalled = true;
                else if (startInfo.Arguments.StartsWith("branch --quiet --set-upstream-to=origin/main", StringComparison.Ordinal))
                    setUpstreamCalled = true;
            }
        );

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, _ => { });

        // Assert
        result.ShouldBeTrue();
        checkoutMainCalled.ShouldBeTrue();
        rebaseAutoStashCalled.ShouldBeTrue();
        setUpstreamCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, false, []);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(false);
        var progressCalled = false;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, _ => progressCalled = true);

        // Assert
        result.ShouldBeFalse();
        progressCalled.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenRepoPathIsNullOrWhitespace_ShouldReturnFalse()
    {
        // Arrange
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, null!, REMOTE_URL, false, []);
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(false);
        var progressCalled = false;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, _ => progressCalled = true);

        // Assert
        result.ShouldBeFalse();
        progressCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenNoLocalBranches_ShouldReturnFalseAndReport()
    {
        // Arrange
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, false, []);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        string? progressMsg = null;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, msg => progressMsg = msg.ToString());

        // Assert
        result.ShouldBeFalse();
        progressMsg.ShouldNotBeNull();
        progressMsg.ShouldContain("no local branches");
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenExceptionOccurs_ShouldReturnFalseAndReport()
    {
        // Arrange
        var repo = new GitRepositoryStatus
        (
            REPO_NAME,
            REPO_NAME,
            REPO_PATH,
            REMOTE_URL,
            false,
            [new(REPO_PATH, "main", "origin/main", true, 0, 0, false, false, DateTime.Now, true)]
        );

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(_ => throw new InvalidOperationException("sync error"));

        string? progressMsg = null;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, msg => progressMsg = msg.ToString());

        // Assert
        result.ShouldBeFalse();
        progressMsg.ShouldNotBeNull();
        progressMsg.ShouldContain("failed");
        _console.Output.ShouldContain("sync error");
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WithUncommittedChanges_ShouldStashAndPop()
    {
        // Arrange
        var branch = new BranchStatus(REPO_PATH, "main", "origin/main", true, 0, 0, false, false, DateTime.Now, true);
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, true, [branch]);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var stashCalled = false;
        var stashPopCalled = false;

        _processRunner.WhenForAnyArgs
        (
            ctx => ctx.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
        ).Do
        (
            info =>
            {
                var startInfo = info.Arg<ProcessStartInfo>();

                if (startInfo.Arguments.StartsWith("stash pop", StringComparison.Ordinal))
                    stashPopCalled = true;
                else if (startInfo.Arguments.StartsWith("stash", StringComparison.Ordinal))
                    stashCalled = true;
            }
        );

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        string? progressMsg = null;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, msg => progressMsg = msg.ToString(), withUncommited: true);

        // Assert
        result.ShouldBeTrue();
        stashCalled.ShouldBeTrue();
        stashPopCalled.ShouldBeTrue();
        progressMsg.ShouldNotBeNull();
        progressMsg.ShouldContain("updated");
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenBranchNotTrackedAndPushNewBranchesFalse_ShouldNotReportFailure()
    {
        // Arrange
        var branch = new BranchStatus(REPO_PATH, "feature", null, true, 0, 0, false, false, DateTime.Now, true);
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, false, [branch]);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        string? progressMsg = null;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, msg => progressMsg = msg.ToString(), pushNewBranches: false);

        // Assert
        result.ShouldBeTrue(); // O método não retorna false, mas reporta falha na branch
        progressMsg.ShouldNotBeNull();
        progressMsg.ShouldNotContain("failed to synchronize branch");
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenBranchTracked_ShouldSynchronizeAndReportSuccess()
    {
        // Arrange
        var branch = new BranchStatus(REPO_PATH, "main", "origin/main", true, 0, 0, false, false, DateTime.Now, true);
        var repo = new GitRepositoryStatus(REPO_NAME, REPO_NAME, REPO_PATH, REMOTE_URL, false, [branch]);
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        string? progressMsg = null;

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repo, msg => progressMsg = msg.ToString());

        // Assert
        result.ShouldBeTrue();
        progressMsg.ShouldNotBeNull();
        progressMsg.ShouldContain("updated");
    }

    [Fact]
    public async Task SynchronizeRepositoryAsync_WhenOtherExceptionOccurs_ShouldReturnFalseAndReport()
    {
        // Arrange
        var repoStatus = new GitRepositoryStatus
        (
            "repo",
            "repo",
            REPO_PATH,
            REMOTE_URL,
            false,
            []
        );

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var shouldThrow = true;

        void Progress(FormattableString msg)
        {
            if (!shouldThrow)
            {
                _console.MarkupLineInterpolated(msg);

                return;
            }

            shouldThrow = false;

            throw new InvalidOperationException("Unexpected error");
        }

        // Act
        var result = await _gitService.SynchronizeRepositoryAsync(repoStatus, Progress);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Unexpected error");
    }

    [Fact]
    public async Task StashAsync_WithoutIncludeUntracked_ShouldExecuteBasicStashCommand()
    {
        // Arrange
        const string GIT_STASH_OUTPUT = "";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_STASH_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.StashAsync(REPO_PATH);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "stash" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task StashAsync_WithIncludeUntracked_ShouldExecuteStashCommandWithUntrackedFlag()
    {
        // Arrange
        const string GIT_STASH_OUTPUT = "";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_STASH_OUTPUT));

                return 0;
            });

        // Act
        var result = await _gitService.StashAsync(REPO_PATH, includeUntracked: true);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "stash --include-untracked" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task StashAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.StashAsync(NON_EXISTENT_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task StashAsync_WithEmptyRepositoryPath_ShouldReturnFalse()
    {
        // Arrange
        const string EMPTY_REPO_PATH = "";

        // Act
        var result = await _gitService.StashAsync(EMPTY_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task StashAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git stash failed";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.StashAsync(REPO_PATH);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error stashing changes");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task GetRepositoryStatusAsync_WhenRepositoryDoesNotExist_ShouldReturnStatusWithError()
    {
        // Arrange
        const string ROOT_DIR = @"C:\repos";
        const string NON_EXISTENT_REPO_PATH = @"C:\repos\nonexistent";
        const string EXPECTED_ERROR = "Repository does not exist.";

        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.GetRepositoryStatusAsync(NON_EXISTENT_REPO_PATH, ROOT_DIR);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("nonexistent");
        result.HierarchicalName.ShouldBe("nonexistent");
        result.RepoPath.ShouldBe(NON_EXISTENT_REPO_PATH);
        result.RemoteUrl.ShouldBeNull();
        result.HasUncommitedChanges.ShouldBeFalse();
        result.LocalBranches.ShouldBeEmpty();
        result.ErrorMessage.ShouldBe(EXPECTED_ERROR);
        result.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public async Task GetRepositoryStatusAsync_WithNullRepositoryPath_ShouldReturnStatusWithError()
    {
        // Arrange
        const string ROOT_DIR = @"C:\repos";
        const string? NULL_REPO_PATH = null;
        const string EXPECTED_ERROR = "Repository does not exist.";

        // Act

        var result = await _gitService.GetRepositoryStatusAsync(NULL_REPO_PATH!, ROOT_DIR);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("");
        result.RepoPath.ShouldBe(NULL_REPO_PATH);
        result.RemoteUrl.ShouldBeNull();
        result.HasUncommitedChanges.ShouldBeFalse();
        result.LocalBranches.ShouldBeEmpty();
        result.ErrorMessage.ShouldBe(EXPECTED_ERROR);
        result.HasErrors.ShouldBeTrue();
    }

    [Fact]
    public async Task GetRepositoryStatusAsync_WhenRepositoryHasNoBranches_ShouldReturnStatusWithError()
    {
        // Arrange
        const string ROOT_DIR = @"C:\repos";
        const string REMOTE_LOCAL_URL = "https://github.com/user/repo.git";
        const string EXPECTED_ERROR = "No local branches found.";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo => MockNoBranchesGitProcess(callInfo, REMOTE_LOCAL_URL));

        // Act
        var result = await _gitService.GetRepositoryStatusAsync(REPO_PATH, ROOT_DIR);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("repo");
        result.RepoPath.ShouldBe(REPO_PATH);
        result.RemoteUrl.ShouldBe(REMOTE_LOCAL_URL);
        result.HasUncommitedChanges.ShouldBeFalse();
        result.LocalBranches.ShouldBeEmpty();
        result.ErrorMessage.ShouldBe(EXPECTED_ERROR);
        result.HasErrors.ShouldBeTrue();
    }

    // ReSharper disable once CyclomaticComplexity
    private static int MockNoBranchesGitProcess(NSubstitute.Core.CallInfo callInfo, string REMOTE_LOCAL_URL)
    {
        var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
        var psi = callInfo.ArgAt<ProcessStartInfo>(0);

        if (psi.Arguments.Contains("config --get remote.origin.url"))
        {
            outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(REMOTE_LOCAL_URL));
        }
        else if (psi.Arguments.Contains("branch --format") || psi.Arguments.Contains("status --porcelain"))
        {
            outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));
        }
        else if (psi.Arguments.Contains("for-each-ref"))
        {
            // Mock upstream tracking for main and develop, but not feature
            if (psi.Arguments.Contains($"refs/heads/{MAIN_BRANCH}"))
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs("origin/main"));
            else if (psi.Arguments.Contains($"refs/heads/{DEVELOP_BRANCH}"))
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs("origin/develop"));
            else
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));
        }
        else if (psi.Arguments.Contains("show-ref"))
        {
            // Mock remote ref existence for tracked branches
            if (psi.Arguments.Contains("origin/main") || psi.Arguments.Contains("origin/develop"))
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs($"abc123 refs/remotes/{psi.Arguments.Split(' ')[1]}"));
            else
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));
        }
        else if (psi.Arguments.Contains("rev-parse --abbrev-ref HEAD"))
        {
            outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(MAIN_BRANCH));
        }
        else if (psi.Arguments.Contains("rev-list --left-right --count"))
        {
            // Mock ahead/behind counts
            if (psi.Arguments.Contains($"{MAIN_BRANCH}...origin/{MAIN_BRANCH}"))
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs("0\t2"));
            else if (psi.Arguments.Contains($"{DEVELOP_BRANCH}...origin/{DEVELOP_BRANCH}"))
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs("1\t0"));
            else
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs("0\t0"));
        }

        return 0;
    }

    [Fact]
    public async Task GetRepositoryStatusAsync_WhenGitCommandFails_ShouldReturnStatusWithError()
    {
        // Arrange
        const string ROOT_DIR = @"C:\repos";
        const string ERROR_MESSAGE = "Git command failed";

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.GetRepositoryStatusAsync(REPO_PATH, ROOT_DIR);        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("repo");
        result.RepoPath.ShouldBe(REPO_PATH);
        result.RemoteUrl.ShouldBeNull();
        result.HasUncommitedChanges.ShouldBeFalse();
        result.LocalBranches.ShouldBeEmpty();
        result.ErrorMessage.ShouldBe(ERROR_MESSAGE);
        result.HasErrors.ShouldBeTrue();
    }

    [Fact]
    // ReSharper disable once CognitiveComplexity
    public async Task GetRepositoryStatusAsync_WithMultipleBranches_ShouldReturnCompleteStatusWithAllBranches()
    {
        // Arrange
        const string ROOT_DIR = @"C:\repos";
        const string REMOTE_URL = "https://github.com/user/repo.git";
        const string FEATURE_BRANCH = "feature/new-feature";
        
        var branches = new List<string> { "main", "develop", FEATURE_BRANCH };

        var upstreams = new Dictionary<string, string>
        {
            ["main"] = "origin/main",
            ["develop"] = "origin/develop"
        };

        var aheadBehind = new Dictionary<string, (int ahead, int behind)>
        {
            ["main"] = (0, 2),
            ["develop"] = (1, 0),
            [FEATURE_BRANCH] = (0, 0)
        };

        var goneBranches = new Dictionary<string, bool>
        {
            [FEATURE_BRANCH] = true
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureRepositoryStatus
        (
            remoteUrl: REMOTE_URL,
            localBranches: branches,
            modifiedFiles: ["modified_file.txt"],
            upstreamBranches: upstreams,
            aheadBehindCounts: aheadBehind,
            goneBranches: goneBranches,
            currentBranch: "main"
        );

        // Act
        var result = await _gitService.GetRepositoryStatusAsync(REPO_PATH, ROOT_DIR);        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("repo");
        result.HierarchicalName.ShouldBe("../test/repo");
        result.RepoPath.ShouldBe(REPO_PATH);
        result.RemoteUrl.ShouldBe(REMOTE_URL);
        result.HasUncommitedChanges.ShouldBeTrue();
        result.LocalBranches.ShouldNotBeEmpty();
        result.LocalBranches.Count.ShouldBe(3);
        result.ErrorMessage.ShouldBeNull();
        result.HasErrors.ShouldBeFalse();

        // Verify main branch details
        var mainBranch = result.LocalBranches.FirstOrDefault(static b => b.Name == MAIN_BRANCH);
        mainBranch.ShouldNotBeNull();
        mainBranch.IsCurrent.ShouldBeTrue();
        mainBranch.IsTracked.ShouldBeTrue();
        mainBranch.Upstream.ShouldBe("origin/main");
        mainBranch.RemoteAheadCount.ShouldBe(0);
        mainBranch.RemoteBehindCount.ShouldBe(2);

        // Verify develop branch details
        var developBranch = result.LocalBranches.FirstOrDefault(static b => b.Name == DEVELOP_BRANCH);
        developBranch.ShouldNotBeNull();
        developBranch.IsCurrent.ShouldBeFalse();
        developBranch.IsTracked.ShouldBeTrue();
        developBranch.Upstream.ShouldBe("origin/develop");
        developBranch.RemoteAheadCount.ShouldBe(1);
        developBranch.RemoteBehindCount.ShouldBe(0);

        // Verify feature branch details
        var featureBranch = result.LocalBranches.FirstOrDefault(static b => b.Name == FEATURE_BRANCH);
        featureBranch.ShouldNotBeNull();
        featureBranch.IsCurrent.ShouldBeFalse();
        featureBranch.IsTracked.ShouldBeFalse();
        featureBranch.Upstream.ShouldBeNull();
        featureBranch.RemoteAheadCount.ShouldBe(0);
        featureBranch.RemoteBehindCount.ShouldBe(0);

        // Verify repository status calculations
        result.TrackedBranchesCount.ShouldBe(2);
        result.UntrackedBranchesCount.ShouldBe(1);
        result.CurrentBranch.ShouldBe(MAIN_BRANCH);
    }

    [Fact]
    public async Task IsCurrentBranchAsync_WhenBranchIsCurrent_ShouldReturnTrue()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(BRANCH_NAME));

                return 0;
            });

        // Act
        var result = await _gitService.IsCurrentBranchAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsCurrentBranchAsync_WhenBranchIsNotCurrent_ShouldReturnFalse()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string CURRENT_BRANCH = "develop";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(CURRENT_BRANCH));

                return 0;
            });

        // Act
        var result = await _gitService.IsCurrentBranchAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsCurrentBranchAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.IsCurrentBranchAsync(NON_EXISTENT_REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task IsCurrentBranchAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string ERROR_MESSAGE = "Git error";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.IsCurrentBranchAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error checking current branch");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task IsBranchTrackedAsync_WhenBranchIsTracked_ShouldReturnTrueAndUpstream()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string UPSTREAM = "origin/main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                if (psi.Arguments.Contains("for-each-ref"))
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(UPSTREAM));
                else if (psi.Arguments.Contains("show-ref"))
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs($"abc123 refs/remotes/{UPSTREAM}"));

                return 0;
            });

        // Act
        var (isTracked, upstream) = await _gitService.IsBranchTrackedAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        isTracked.ShouldBeTrue();
        upstream.ShouldBe(UPSTREAM);
    }

    [Fact]
    public async Task IsBranchTrackedAsync_WhenBranchIsNotTracked_ShouldReturnFalseAndNull()
    {
        // Arrange
        const string BRANCH_NAME = "feature/no-upstream";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                var psi = callInfo.ArgAt<ProcessStartInfo>(0);

                if (psi.Arguments.Contains("for-each-ref"))
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));
                else if (psi.Arguments.Contains("show-ref"))
                    outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

                return 0;
            });

        // Act
        var (isTracked, upstream) = await _gitService.IsBranchTrackedAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        isTracked.ShouldBeFalse();
        upstream.ShouldBeNull();
    }

    [Fact]
    public async Task IsBranchTrackedAsync_WhenRepositoryDoesNotExist_ShouldReturnFalseAndNull()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var (isTracked, upstream) = await _gitService.IsBranchTrackedAsync(NON_EXISTENT_REPO_PATH, BRANCH_NAME);

        // Assert
        isTracked.ShouldBeFalse();
        upstream.ShouldBeNull();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task IsBranchTrackedAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string ERROR_MESSAGE = "Git error";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var (isTracked, upstream) = await _gitService.IsBranchTrackedAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        isTracked.ShouldBeFalse();
        upstream.ShouldBeNull();
        _console.Output.ShouldContain("Error checking if branch");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task PushAsync_Default_ShouldPushBranchAndTags()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var callCount = 0;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(_ =>
            {
                callCount++;

                return 0;
            });

        // Act
        var result = await _gitService.PushAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeTrue();
        callCount.ShouldBe(2); // push branch + push --tags

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments.Contains("push") &&
                psi.Arguments.Contains(BRANCH_NAME)),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments.Contains("push --tags")),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PushAsync_WithForce_ShouldPushWithForceFlag()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var callCount = 0;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(_ =>
            {
                callCount++;

                return 0;
            });

        // Act
        var result = await _gitService.PushAsync(REPO_PATH, BRANCH_NAME, force: true);

        // Assert
        result.ShouldBeTrue();
        callCount.ShouldBe(2); // push branch + push --tags

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments.Contains("push --force") &&
                psi.Arguments.Contains(BRANCH_NAME)),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PushAsync_WithoutTags_ShouldNotPushTags()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var callCount = 0;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(_ =>
            {
                callCount++;

                return 0;
            });

        // Act
        var result = await _gitService.PushAsync(REPO_PATH, BRANCH_NAME, tags: false);

        // Assert
        result.ShouldBeTrue();
        callCount.ShouldBe(1); // apenas push branch

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments.Contains("push") &&
                psi.Arguments.Contains(BRANCH_NAME)),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PushAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.PushAsync(NON_EXISTENT_REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PushAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string BRANCH_NAME = "main";
        const string ERROR_MESSAGE = "Git push failed";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.PushAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error pushing changes");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task CheckoutAsync_WhenBranchIsValid_ShouldRunGitCheckout()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);
        var called = false;

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(_ => { called = true; return 0; });

        // Act
        await _gitService.CheckoutAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        called.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"checkout {BRANCH_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckoutAsync_WhenBranchIsNullOrWhitespace_ShouldNotRunGitCheckout(string? branch)
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        // Act
        await _gitService.CheckoutAsync(REPO_PATH, branch);

        // Assert
        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task CheckoutAsync_WhenRepositoryDoesNotExist_ShouldNotRunGitCheckout()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(false);

        // Act
        await _gitService.CheckoutAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        await _processRunner.DidNotReceive().RunAsync(
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task CheckoutAsync_WhenExceptionOccurs_ShouldLogError()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test";
        const string ERROR_MESSAGE = "Checkout failed";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        await _gitService.CheckoutAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        _console.Output.ShouldContain($"Error checking out branch {BRANCH_NAME}");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task GetLocalBranchesAsync_WhenRepositoryHasBranches_ShouldReturnBranchList()
    {
        // Arrange
        const string GIT_OUTPUT = "main\ndevelop\nfeature/test-1\n";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns(static callInfo =>
        {
            var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
            outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

            return 0;
        });

        // Act
        var result = await _gitService.GetLocalBranchesAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(3);
        result.ShouldContain("main");
        result.ShouldContain("develop");
        result.ShouldContain("feature/test-1");
    }

    [Fact]
    public async Task GetLocalBranchesAsync_WhenRepositoryHasNoBranches_ShouldReturnEmptyList()
    {
        // Arrange
        _fileSystem.Directory.Exists(Arg.Any<string>()).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns(static callInfo =>
        {
            var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
            outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

            return 0;
        });

        // Act
        var result = await _gitService.GetLocalBranchesAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetLocalBranchesAsync_WhenBranchesHaveQuotes_ShouldReturnTrimmedNames()
    {
        // Arrange
        const string GIT_OUTPUT = "'main'\n'feature/with space'\n'bugfix'\n";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns(static callInfo =>
        {
            var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
            outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(GIT_OUTPUT));

            return 0;
        });

        // Act
        var result = await _gitService.GetLocalBranchesAsync(REPO_PATH);

        // Assert
        result.ShouldContain("main");
        result.ShouldContain("feature/with space");
        result.ShouldContain("bugfix");
    }

    [Fact]
    public async Task GetLocalBranchesAsync_WhenRepositoryDoesNotExist_ShouldReturnEmptyList()
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.GetLocalBranchesAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetLocalBranchesAsync_WhenGitCommandFails_ShouldReturnEmptyListAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "git error";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.GetLocalBranchesAsync(REPO_PATH);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
        _console.Output.ShouldContain("Error getting local branches");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task PopAsync_WhenRepositoryDoesNotExist_ShouldReturnFalse()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.PopAsync(NON_EXISTENT_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PopAsync_WithEmptyRepositoryPath_ShouldReturnFalse()
    {
        // Arrange
        const string EMPTY_REPO_PATH = "";

        // Act
        var result = await _gitService.PopAsync(EMPTY_REPO_PATH);

        // Assert
        result.ShouldBeFalse();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PopAsync_WhenGitCommandSucceeds_ShouldReturnTrue()
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns(0);

        // Act
        var result = await _gitService.PopAsync(REPO_PATH);

        // Assert
        result.ShouldBeTrue();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "stash pop" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task PopAsync_WhenGitCommandFails_ShouldReturnFalseAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git stash pop failed";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        ).Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.PopAsync(REPO_PATH);

        // Assert
        result.ShouldBeFalse();
        _console.Output.ShouldContain("Error poping stashing changes");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }
    
    [Fact]
    public async Task GetCurrentBranchAsync_WhenBranchExists_ShouldReturnBranchName()
    {
        // Arrange
        const string CURRENT_BRANCH = "main";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(CURRENT_BRANCH));

                return 0;
            });

        // Act
        var result = await _gitService.GetCurrentBranchAsync(REPO_PATH);

        // Assert
        result.ShouldBe(CURRENT_BRANCH);

        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(static psi =>
                psi.FileName == "git" &&
                psi.Arguments == "rev-parse --abbrev-ref HEAD" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenBranchHasWhitespace_ShouldReturnTrimmedBranchName()
    {
        // Arrange
        const string CURRENT_BRANCH = "  feature/test-branch  ";
        const string EXPECTED_BRANCH = "feature/test-branch";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(CURRENT_BRANCH));

                return 0;
            });

        // Act
        var result = await _gitService.GetCurrentBranchAsync(REPO_PATH);

        // Assert
        result.ShouldBe(EXPECTED_BRANCH);
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenOutputIsEmpty_ShouldReturnNull()
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(""));

                return 0;
            });

        // Act
        var result = await _gitService.GetCurrentBranchAsync(REPO_PATH);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenOutputIsWhitespace_ShouldReturnNull()
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(static callInfo =>
            {
                var outputHandler = callInfo.ArgAt<DataReceivedEventHandler>(1);
                outputHandler?.Invoke(null!, CreateDataReceivedEventArgs("   "));

                return 0;
            });

        // Act
        var result = await _gitService.GetCurrentBranchAsync(REPO_PATH);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenRepositoryPathIsNull_ShouldReturnNull()
    {
        // Act
        var result = await _gitService.GetCurrentBranchAsync(null!);

        // Assert
        result.ShouldBeNull();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenRepositoryPathIsEmpty_ShouldReturnNull()
    {
        // Act
        var result = await _gitService.GetCurrentBranchAsync(string.Empty);

        // Assert
        result.ShouldBeNull();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenRepositoryDoesNotExist_ShouldReturnNull()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.GetCurrentBranchAsync(NON_EXISTENT_REPO_PATH);

        // Assert
        result.ShouldBeNull();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetCurrentBranchAsync_WhenGitCommandFails_ShouldReturnNullAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git error occurred";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.GetCurrentBranchAsync(REPO_PATH);

        // Assert
        result.ShouldBeNull();
        _console.Output.ShouldContain("Error retrieving current branch");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_ShouldExcludeProtectedBranches()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/old" };
        var mergedBranches = new List<string> { "main", "feature/old" };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            mergedBranches: mergedBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, olderThanDays: null);

        // Assert
        result.ShouldNotContain(b => b.Name == "main");
        result.ShouldContain(b => b.Name == "feature/old");
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenRepositoryPathIsNull_ShouldReturnEmptyList()
    {
        // Act
        var result = await _gitService.GetPrunableBranchesAsync(null!, merged: true, gone: false, olderThanDays: null);

        // Assert
        result.ShouldBeEmpty();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenRepositoryPathIsEmpty_ShouldReturnEmptyList()
    {
        // Act
        var result = await _gitService.GetPrunableBranchesAsync(string.Empty, merged: true, gone: false, olderThanDays: null);

        // Assert
        result.ShouldBeEmpty();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenRepositoryDoesNotExist_ShouldReturnEmptyList()
    {
        // Arrange
        const string NON_EXISTENT_REPO_PATH = @"C:\non\existent\repo";
        _fileSystem.Directory.Exists(NON_EXISTENT_REPO_PATH).Returns(false);

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(NON_EXISTENT_REPO_PATH, merged: true, gone: false, olderThanDays: null);

        // Assert
        result.ShouldBeEmpty();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenGitCommandFails_ShouldReturnEmptyListAndLogError()
    {
        // Arrange
        const string ERROR_MESSAGE = "Git command failed";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(static _ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, olderThanDays: null);

        // Assert
        result.ShouldBeEmpty();
        _console.Output.ShouldContain("Error getting local branches");
        _console.Output.ShouldContain(REPO_PATH);
        _console.Output.ShouldContain(ERROR_MESSAGE);
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WithMergedOption_ShouldReturnBranches()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/one", "feature/two" };
        var mergedBranches = new List<string> { "feature/one", "feature/two" };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            mergedBranches: mergedBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, olderThanDays: null);

        // Assert
        result.ShouldContain(b => b.Name == "feature/one");
        result.ShouldContain(b => b.Name == "feature/two");
        result.ShouldNotContain(b => b.Name == "main");
        result.ShouldNotContain(b => b.Name == "develop");
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenMergedBranchesRequested_ShouldReturnMergedBranches()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/branch1", "feature/branch2" };
        var mergedBranches = new List<string> { "feature/branch1", "feature/branch2", "main" };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            mergedBranches: mergedBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, olderThanDays: null);

        // Assert
        result.ShouldContain(b => b.Name == "feature/branch1");
        result.ShouldContain(b => b.Name == "feature/branch2");
        result.ShouldNotContain(b => b.Name == "main"); // Protected branch
        result.ShouldNotContain(b => b.Name == "master"); // Protected branch
        result.ShouldNotContain(b => b.Name == "develop"); // Protected branch
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenGoneBranchesRequested_ShouldReturnGoneBranches()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/gone1", "feature/gone2" };

        var goneBranches = new Dictionary<string, bool>
        {
            ["feature/gone1"] = true,
            ["feature/gone2"] = true,
            ["main"] = false
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            goneBranches: goneBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: false, gone: true, olderThanDays: null);

        // Assert
        result.ShouldContain(b => b.Name == "feature/gone1");
        result.ShouldContain(b => b.Name == "feature/gone2");
        result.ShouldNotContain(b => b.Name == "main");
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenOlderThanDaysRequested_ShouldReturnOldBranches()
    {
        // Arrange
        const int OLDER_THAN_DAYS = 30;
        var branches = new List<string> { "main", "feature/old-branch", "feature/recent-branch" };
        var lastCommitDates = new Dictionary<string, DateTime>
        {
            ["main"] = DateTime.UtcNow.AddDays(-40),
            ["feature/old-branch"] = DateTime.UtcNow.AddDays(-40),
            ["feature/recent-branch"] = DateTime.UtcNow.AddDays(-10)
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            lastCommitDates: lastCommitDates
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: false, gone: false, olderThanDays: OLDER_THAN_DAYS);

        // Assert
        result.ShouldContain(b => b.Name == "feature/old-branch");
        result.ShouldNotContain(b => b.Name == "feature/recent-branch");
        result.ShouldNotContain(b => b.Name == "main"); // Protected branch
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenMultipleCriteriaProvided_ShouldReturnUnionOfResults()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/merged1", "feature/merged2", "feature/gone1", "feature/old-branch" };
        var mergedBranches = new List<string> { "feature/merged1", "feature/merged2" };

        var goneBranches = new Dictionary<string, bool>
        {
            ["feature/gone1"] = true
        };

        var lastCommitDates = new Dictionary<string, DateTime>
        {
            ["feature/old-branch"] = DateTime.UtcNow.AddDays(-40)
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            mergedBranches: mergedBranches,
            goneBranches: goneBranches,
            lastCommitDates: lastCommitDates
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: true, olderThanDays: 30);

        // Assert
        result.ShouldContain(b => b.Name == "feature/merged1");
        result.ShouldContain(b => b.Name == "feature/merged2");
        result.ShouldContain(b => b.Name == "feature/gone1");
        result.ShouldContain(b => b.Name == "feature/old-branch");
        result.Count.ShouldBe(4);
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WhenCurrentBranchIsInResults_ShouldExcludeCurrentBranch()
    {
        // Arrange
        const string CURRENT_BRANCH = "feature/current";
        var branches = new List<string> { "main", "feature/normal-branch", "feature/current" };
        var mergedBranches = new List<string> { "main", "feature/normal-branch", "feature/current" };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: CURRENT_BRANCH,
            mergedBranches: mergedBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, olderThanDays: null);

        // Assert
        result.ShouldContain(b => b.Name == "feature/normal-branch");
        result.ShouldNotContain(b => b.Name == "main"); // Protected branch
        result.ShouldNotContain(b => b.Name == "feature/current"); // Current branch should be filtered out
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WithDetachedHeadBranches_ShouldFilterOutDetachedHeads()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/normal-branch" }; // GetLocalBranchesAsync já filtra detached heads
        var mergedBranches = new List<string> { "main", "feature/normal-branch" };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            mergedBranches: mergedBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: true, gone: false, olderThanDays: null);

        // Assert
        result.ShouldContain(b => b.Name == "feature/normal-branch");
        result.ShouldNotContain(b => b.Name == "main"); // Protected branch
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WithGoneBranchesIncludingDetached_ShouldFilterOutDetachedHeads()
    {
        // Arrange
        var branches = new List<string> { "main", "feature/gone1", "feature/gone2" }; // Detached heads não aparecem em GetLocalBranchesAsync
        var goneBranches = new Dictionary<string, bool>
        {
            ["feature/gone1"] = true,
            ["feature/gone2"] = true,
            ["main"] = false
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            goneBranches: goneBranches
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: false, gone: true, olderThanDays: null);

        // Assert
        result.ShouldContain(b => b.Name == "feature/gone1");
        result.ShouldContain(b => b.Name == "feature/gone2");
        result.ShouldNotContain(b => b.Name == "main");
    }

    [Fact]
    public async Task GetPrunableBranchesAsync_WithOlderThanAndDetachedHead_ShouldFilterOutDetachedHead()
    {
        // Arrange
        const int OLDER_THAN_DAYS = 30;
        const string OLD_NORMAL_BRANCH = "feature/old-branch";
        var branches = new List<string> { "main", OLD_NORMAL_BRANCH }; // Detached heads não aparecem em GetLocalBranchesAsync

        var threshold = DateTime.UtcNow.AddDays(-OLDER_THAN_DAYS - 1);
        var lastCommitDates = new Dictionary<string, DateTime>
        {
            [OLD_NORMAL_BRANCH] = threshold,
            ["main"] = threshold
        };

        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        ConfigureBranchStatus
        (
            localBranches: branches,
            currentBranch: "main",
            lastCommitDates: lastCommitDates
        );

        // Act
        var result = await _gitService.GetPrunableBranchesAsync(REPO_PATH, merged: false, gone: false, olderThanDays: OLDER_THAN_DAYS);

        // Assert
        result.ShouldContain(b => b.Name == OLD_NORMAL_BRANCH);
        result.ShouldNotContain(b => b.Name == "main"); // Protected branch
        result.Count.ShouldBe(1);
    }
   
    [Fact]
    public async Task DeleteLocalBranchAsync_WhenBranchExists_ShouldCallGitBranchDelete()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test-branch";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.DeleteLocalBranchAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"branch -d {BRANCH_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteLocalBranchAsync_WhenForceIsTrue_ShouldCallGitBranchDeleteWithForceFlag()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test-branch";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.DeleteLocalBranchAsync(REPO_PATH, BRANCH_NAME, force: true);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"branch -D {BRANCH_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteLocalBranchAsync_WhenRepositoryPathIsNull_ShouldThrowException()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test-branch";

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentNullException>(
            () => _gitService.DeleteLocalBranchAsync(null!, BRANCH_NAME));

        exception.ShouldNotBeNull();

        await _processRunner.DidNotReceive().RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteLocalBranchAsync_WhenBranchNameIsNullOrWhitespace_ShouldCallGitWithInvalidBranch(string? branchName)
    {
        // Arrange
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(_ => throw new InvalidOperationException("error: branch '' not found."));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _gitService.DeleteLocalBranchAsync(REPO_PATH, branchName!));

        exception.ShouldNotBeNull();

        await _processRunner.Received(1).RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteLocalBranchAsync_WhenGitCommandFails_ShouldThrowException()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test-branch";
        const string ERROR_MESSAGE = "error: branch 'feature/test-branch' not found.";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns<int>(_ => throw new InvalidOperationException(ERROR_MESSAGE));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _gitService.DeleteLocalBranchAsync(REPO_PATH, BRANCH_NAME));

        exception.Message.ShouldBe(ERROR_MESSAGE);

        await _processRunner.Received(1).RunAsync
        (
            Arg.Any<ProcessStartInfo>(),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }

    [Fact]
    public async Task DeleteLocalBranchAsync_WhenBranchHasSpecialCharacters_ShouldPassCorrectArguments()
    {
        // Arrange
        const string BRANCH_NAME = "feature/test-branch-with-special#chars";
        _fileSystem.Directory.Exists(REPO_PATH).Returns(true);

        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(0);

        // Act
        await _gitService.DeleteLocalBranchAsync(REPO_PATH, BRANCH_NAME);

        // Assert
        await _processRunner.Received(1).RunAsync
        (
            Arg.Is<ProcessStartInfo>(psi =>
                psi.FileName == "git" &&
                psi.Arguments == $"branch -d {BRANCH_NAME}" &&
                psi.WorkingDirectory == REPO_PATH),
            Arg.Any<DataReceivedEventHandler>(),
            Arg.Any<DataReceivedEventHandler>()
        );
    }
}
