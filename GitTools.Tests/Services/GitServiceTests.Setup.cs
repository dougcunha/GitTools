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

[ExcludeFromCodeCoverage]
public sealed partial class GitServiceTests
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
    private static async IAsyncEnumerable<string> MockReadLinesAsync(string content)
    {
        foreach (var line in content.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            await Task.Yield();

            yield return line;
        }
    }

}
