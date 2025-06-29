using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace GitTools.Tests.Utils;

/// <summary>
/// Configuration options for Git command responses.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GitCommandConfiguratorOptions
{
    public string? RemoteUrl { get; init; }
    public List<string> LocalBranches { get; init; } = [];
    public List<string>? ModifiedFiles { get; init; }
    public string? CurrentBranch { get; init; }
    public Dictionary<string, string>? UpstreamBranches { get; init; }
    public Dictionary<string, (int ahead, int behind)>? AheadBehindCounts { get; init; }
    public Dictionary<string, bool>? GoneBranches { get; init; }
    public Dictionary<string, DateTime>? LastCommitDates { get; init; }
    public List<string>? MergedBranches { get; init; }
}

/// <summary>
/// Constants for Git commands and their patterns.
/// </summary>
[ExcludeFromCodeCoverage]
public static class GitCommands
{
    public const string CONFIG_GET_REMOTE_URL = "config --get remote.origin.url";
    public const string BRANCH_FORMAT = "branch --format='%(refname:short)'";
    public const string STATUS_PORCELAIN = "status --porcelain";
    public const string REV_PARSE_HEAD = "rev-parse --abbrev-ref HEAD";
    public const string BRANCH_MERGED = "branch --merged";
    public const string FOR_EACH_REF_UPSTREAM = "for-each-ref";
    public const string SHOW_REF_ORIGIN = "show-ref origin/";
    public const string REV_LIST_COUNT = "rev-list --left-right --count";
    public const string BRANCH_VV = "branch -vv";
    public const string LOG_FORMAT_DATE = "log -1 --format=%cd";
    public const string FETCH = "fetch";

    public static readonly string[] UPSTREAM_PATTERNS = ["%(upstream:short)"];
    public static readonly string[] DATE_PATTERNS = ["--date=format:'%Y-%m-%d %H:%M:%S'"];
}

/// <summary>
/// Configures Git command responses for testing purposes.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class GitCommandConfigurator
{
    private readonly GitCommandConfiguratorOptions _options;

    public GitCommandConfigurator(GitCommandConfiguratorOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Processes a Git command and returns the appropriate response.
    /// </summary>
    /// <param name="args">The Git command arguments.</param>
    /// <param name="outputHandler">The output handler to invoke with the response.</param>
    /// <returns>The exit code for the command.</returns>
    public int ProcessCommand(string args, DataReceivedEventHandler? outputHandler)
    {
        var response = args switch
        {
            var cmd when cmd.Contains(GitCommands.CONFIG_GET_REMOTE_URL) => HandleRemoteUrl(),
            var cmd when cmd.Contains(GitCommands.BRANCH_FORMAT) => HandleLocalBranches(),
            var cmd when cmd.Contains(GitCommands.STATUS_PORCELAIN) => HandleModifiedFiles(),
            var cmd when cmd.Contains(GitCommands.REV_PARSE_HEAD) => HandleCurrentBranch(),
            var cmd when cmd.Contains(GitCommands.BRANCH_MERGED) => HandleMergedBranches(),
            var cmd when cmd.Contains(GitCommands.FOR_EACH_REF_UPSTREAM) && GitCommands.UPSTREAM_PATTERNS.Any(cmd.Contains) => HandleUpstreamTracking(args),
            var cmd when cmd.Contains(GitCommands.SHOW_REF_ORIGIN) => HandleRemoteRefExistence(args),
            var cmd when cmd.Contains(GitCommands.REV_LIST_COUNT) => HandleAheadBehindCounts(args),
            var cmd when cmd.Contains(GitCommands.BRANCH_VV) => HandleGoneBranches(),
            var cmd when cmd.Contains(GitCommands.LOG_FORMAT_DATE) => HandleLastCommitDate(args),
            var cmd when cmd.Contains(GitCommands.FETCH) => HandleFetch(),
            _ => ""
        };

        outputHandler?.Invoke(null!, CreateDataReceivedEventArgs(response));

        return 0;
    }

    private static DataReceivedEventArgs CreateDataReceivedEventArgs(string data)
    {
        var constructor = typeof(DataReceivedEventArgs)
            .GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(string)], null);

        return (DataReceivedEventArgs)constructor!.Invoke([data]);
    }

    private string HandleRemoteUrl()
        => _options.RemoteUrl ?? "";

    private string HandleLocalBranches()
    {
        if (_options.LocalBranches.Count == 0)
        {
            return "";
        }

        return string.Join("\n", _options.LocalBranches.Select(static b => $"'{b}'"));
    }

    private string HandleModifiedFiles()
    {
        if (_options.ModifiedFiles == null || _options.ModifiedFiles.Count == 0)
        {
            return "";
        }

        return string.Join("\n", _options.ModifiedFiles.Select(static f => $"M {f}"));
    }

    private string HandleCurrentBranch()
        => _options.CurrentBranch ?? "main";

    private string HandleMergedBranches()
    {
        var branches = _options.MergedBranches ?? _options.LocalBranches;

        if (branches.Count == 0)
        {
            return "";
        }

        return string.Join("\n", branches.Select(static b => $"'{b}'"));
    }

    private string HandleUpstreamTracking(string args)
    {
        var branchName = ExtractBranchNameFromForEachRef(args);

        if (!string.IsNullOrEmpty(branchName) &&
            _options.UpstreamBranches?.TryGetValue(branchName, out var upstream) == true)
        {
            return upstream;
        }

        return "";
    }

    private string HandleRemoteRefExistence(string args)
    {
        var branchName = ExtractBranchNameFromShowRef(args);

        if (!string.IsNullOrEmpty(branchName) &&
            _options.UpstreamBranches?.ContainsKey(branchName) == true)
        {
            return $"abc123 refs/remotes/origin/{branchName}";
        }

        return "";
    }

    private string HandleAheadBehindCounts(string args)
    {
        var branchName = ExtractBranchNameFromRevList(args);

        if (!string.IsNullOrEmpty(branchName) &&
            _options.AheadBehindCounts?.TryGetValue(branchName, out var counts) == true)
        {
            return $"{counts.ahead}\t{counts.behind}";
        }

        return "0\t0";
    }

    private string HandleGoneBranches()
    {
        var output = new StringBuilder();

        foreach (var branch in _options.LocalBranches)
        {
            var isGone = _options.GoneBranches?.TryGetValue(branch, out var gone) == true && gone;
            var status = isGone ? "[origin/branch: gone]" : "[origin/branch]";

            output.AppendLine($"  {branch}    abc123 {status} Last commit");
        }

        return output.ToString().Trim();
    }

    private string HandleLastCommitDate(string args)
    {
        var branchName = ExtractBranchNameFromLog(args);

        if (!string.IsNullOrEmpty(branchName) &&
            _options.LastCommitDates?.TryGetValue(branchName, out var date) == true)
        {
            return date.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string HandleFetch()
        => "";

    private static string? ExtractBranchNameFromForEachRef(string args)
    {
        var match = Regex.Match(args, @"refs/heads/(\S+)");

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractBranchNameFromShowRef(string args)
    {
        var match = Regex.Match(args, @"show-ref origin/(\S+)");

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractBranchNameFromRevList(string args)
    {
        var match = Regex.Match(args, @"(\w+)\.\.\.origin/\w+");

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractBranchNameFromLog(string args)
    {
        var parts = args.Split(' ');

        return parts.Length > 0 ? parts[^2] : null;
    }
}