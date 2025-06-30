using System.Diagnostics;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reflection;
using GitTools.Models;
using GitTools.Services;
using GitTools.Tests.Utils;
using Spectre.Console;
using Spectre.Console.Testing;

namespace GitTools.Tests.Services;

public sealed partial class GitServiceTests
{
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
}
