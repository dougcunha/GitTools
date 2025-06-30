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

    public static readonly string[] Upstream_Patterns = ["%(upstream:short)"];
    public static readonly string[] Date_Patterns = ["--date=format:'%Y-%m-%d %H:%M:%S'"];
}

/// <summary>
/// Configures Git command responses for testing purposes.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed partial class GitCommandConfigurator(GitCommandConfiguratorOptions options)
{
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
            var cmd when cmd.Contains(GitCommands.FOR_EACH_REF_UPSTREAM) && GitCommands.Upstream_Patterns.Any(cmd.Contains) => HandleUpstreamTracking(args),
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
        => options.RemoteUrl ?? "";

    private string HandleLocalBranches()
        => options.LocalBranches.Count == 0
            ? ""
            : string.Join("\n", options.LocalBranches.Select(static b => $"'{b}'"));

    private string HandleModifiedFiles()
    {
        return options.ModifiedFiles == null || options.ModifiedFiles.Count == 0
            ? ""
            : string.Join("\n", options.ModifiedFiles.Select(static f => $"M {f}"));
    }

    private string HandleCurrentBranch()
        => options.CurrentBranch ?? "main";

    private string HandleMergedBranches()
    {
        var branches = options.MergedBranches ?? options.LocalBranches;

        return branches.Count == 0
            ? ""
            : string.Join("\n", branches.Select(static b => $"'{b}'"));
    }

    private string HandleUpstreamTracking(string args)
    {
        var branchName = ExtractBranchNameFromForEachRef(args);

        return !string.IsNullOrEmpty(branchName)
            && options.UpstreamBranches?.TryGetValue(branchName, out var upstream) == true
                ? upstream
                : "";
    }

    private string HandleRemoteRefExistence(string args)
    {
        var branchName = ExtractBranchNameFromShowRef(args);

        return !string.IsNullOrEmpty(branchName) && options.UpstreamBranches?.ContainsKey(branchName) == true
            ? $"abc123 refs/remotes/origin/{branchName}"
            : "";
    }

    private string HandleAheadBehindCounts(string args)
    {
        var branchName = ExtractBranchNameFromRevList(args);

        return !string.IsNullOrEmpty(branchName)
            && options.AheadBehindCounts?.TryGetValue(branchName, out var counts) == true
                ? $"{counts.ahead}\t{counts.behind}"
                : "0\t0";
    }

    private string HandleGoneBranches()
    {
        var output = new StringBuilder();

        foreach (var branch in options.LocalBranches)
        {
            var isGone = options.GoneBranches?.TryGetValue(branch, out var gone) == true && gone;
            var status = isGone ? "[origin/branch: gone]" : "[origin/branch]";

            output.AppendLine($"  {branch}    abc123 {status} Last commit");
        }

        return output.ToString().Trim();
    }

    private string HandleLastCommitDate(string args)
    {
        var branchName = ExtractBranchNameFromLog(args);

        return !string.IsNullOrEmpty(branchName)
            && options.LastCommitDates?.TryGetValue(branchName, out var date) == true
                ? date.ToString("yyyy-MM-dd HH:mm:ss")
                : DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string HandleFetch()
        => "";

    private static string? ExtractBranchNameFromForEachRef(string args)
    {
        var match = BranchNameFromForEachRefRegex().Match(args);

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractBranchNameFromShowRef(string args)
    {
        var match = BranchNameFromShowRefRegex().Match(args);

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractBranchNameFromRevList(string args)
    {
        var match = BranchNameFromRevListRegex().Match(args);

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractBranchNameFromLog(string args)
    {
        var parts = args.Split(' ');

        return parts.Length > 0 ? parts[^2] : null;
    }

    [GeneratedRegex(@"refs/heads/(\S+)")]
    private static partial Regex BranchNameFromForEachRefRegex();

    [GeneratedRegex(@"show-ref origin/(\S+)")]
    private static partial Regex BranchNameFromShowRefRegex();

    [GeneratedRegex(@"(\w+)\.\.\.origin/\w+")]
    private static partial Regex BranchNameFromRevListRegex();
}