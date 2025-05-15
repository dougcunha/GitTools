using System.Diagnostics;
using System.Text;

namespace GitTools;

/// <summary>
/// Provides tag-related operations for git repositories.
/// </summary>
public sealed class GitService
{
    /// <summary>
    /// Checks if the repository has the given tag.
    /// </summary>
    public async Task<bool> HasTagAsync(string repoPath, string tag)
    {
        var result = await RunGitCommandAsync(repoPath, $"tag -l {tag}");

        return !string.IsNullOrWhiteSpace(result);
    }

    /// <summary>
    /// Removes the given tag from the repository.
    /// </summary>
    public async Task DeleteTagAsync(string repoPath, string tag)
        => await RunGitCommandAsync(repoPath, $"tag -d {tag}");

    /// <summary>
    /// Removes the given tag from the remote repository.
    /// </summary>
    public async Task DeleteRemoteTagAsync(string repoPath, string tag)
        => await RunGitCommandAsync(repoPath, $"push origin :refs/tags/{tag}");

    /// <summary>
    /// Runs a git command in the correct repository directory.
    /// </summary>
    public async Task<string> RunGitCommandAsync(string workingDirectory, string arguments)
    {
        var realWorkingDirectory = GetRealGitDirectory(workingDirectory);

        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = realWorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new Exception(error.ToString());

        return output.ToString();
    }

    private static string GetRealGitDirectory(string repoPath)
    {
        const string GIT_DIR = ".git";
        var gitPath = Path.Combine(repoPath, GIT_DIR);

        if (Directory.Exists(gitPath))
            return repoPath;

        if (File.Exists(gitPath))
        {
            var content = File.ReadAllText(gitPath).Trim();
            if (content.StartsWith("gitdir:"))
            {
                var relativePath = content[7..].Trim();
                var fullPath = Path.GetFullPath(Path.Combine(repoPath, relativePath));
                return fullPath;
            }
        }
        return repoPath;
    }
}
