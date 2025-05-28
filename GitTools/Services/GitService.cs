using System.Diagnostics;
using System.IO.Abstractions;
using System.Text;

namespace GitTools.Services;

/// <summary>
/// Provides tag-related operations for git repositories.
/// </summary>
public sealed class GitService(IFileSystem fileSystem, IProcessRunner processRunner) : IGitService
{
    /// <inheritdoc/>
    public async Task<bool> HasTagAsync(string repoPath, string tag)
    {
        var result = await RunGitCommandAsync(repoPath, $"tag -l {tag}");

        return !string.IsNullOrWhiteSpace(result);
    }

    /// <inheritdoc/>
    public async Task DeleteTagAsync(string repoPath, string tag)
        => await RunGitCommandAsync(repoPath, $"tag -d {tag}");

    /// <inheritdoc/>
    public async Task DeleteRemoteTagAsync(string repoPath, string tag)
        => await RunGitCommandAsync(repoPath, $"push origin :refs/tags/{tag}");

    /// <inheritdoc/>
    public async Task<string> RunGitCommandAsync(string workingDirectory, string arguments)
    {
        var realWorkingDirectory = GetRealGitDirectory(workingDirectory);

        var output = new StringBuilder();
        var error = new StringBuilder();

        var exitCode = await processRunner.RunAsync
        (
            new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = realWorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            (s, e) => { if (e.Data != null) output.AppendLine(e.Data); },
            (s, e) => { if (e.Data != null) error.AppendLine(e.Data); }
        ).ConfigureAwait(false);

        if (exitCode != 0)
            throw new Exception(error.ToString());

        return output.ToString();
    }

    /// <summary>
    /// Gets the real git directory for the specified repository path.
    /// This method checks if the .git directory exists directly in the repository path,
    /// or if it's a git worktree, it will find the main repository directory.
    /// </summary>
    /// <param name="repoPath">
    /// The path to the git repository.
    /// </param>
    /// <returns>
    /// The path to the real git directory.
    /// </returns>
    private string GetRealGitDirectory(string repoPath)
    {
        const string GIT_DIR = ".git";
        var gitPath = Path.Combine(repoPath, GIT_DIR);

        if (fileSystem.Directory.Exists(gitPath))
            return repoPath;

        if (fileSystem.File.Exists(gitPath))
        {
            var content = fileSystem.File.ReadAllText(gitPath).Trim();

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
