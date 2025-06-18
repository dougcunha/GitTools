using Spectre.Console;

namespace GitTools.Services;

/// <summary>
/// Provides console display operations for git repository operations.
/// </summary>
public sealed class ConsoleDisplayService(IAnsiConsole console) : IConsoleDisplayService
{
    /// <inheritdoc />
    public string GetHierarchicalName(string repositoryPath, string baseFolder)
    {
        var relativePath = Path.GetRelativePath(baseFolder, repositoryPath)
            .Replace(Path.DirectorySeparatorChar, '/');

        return relativePath.Length <= 1
            ? Path.GetFileName(repositoryPath)
            : relativePath;
    }

    /// <inheritdoc />
    public void ShowScanErrors(Dictionary<string, Exception> scanErrors, string baseFolder)
    {

        if (scanErrors.Count == 0 || !console.Confirm($"[red]{scanErrors.Count} scan errors detected. Do you want to see the details?[/]"))
            return;
            
        foreach (var (repoPath, exception) in scanErrors)
        {
            var rule = new Rule($"[red]{GetHierarchicalName(repoPath, baseFolder)}[/]")
                .RuleStyle(new Style(Color.Red))
                .Centered();

            console.Write(rule);            

            console.WriteException
            (
                exception,
                string.IsNullOrWhiteSpace(exception.StackTrace) // Avoid IndexOutOfRangeException inside Spectre.Console
                    ? ExceptionFormats.ShortenEverything | ExceptionFormats.NoStackTrace
                    : ExceptionFormats.ShortenEverything 
            );
        }
    }

    /// <inheritdoc />
    public void ShowInitialInfo(string baseFolder, string[] tags)
    {
        console.MarkupLineInterpolated($"[blue]Base folder: [bold]{baseFolder}[/][/]");
        console.MarkupLineInterpolated($"[blue]Tags to search: [bold]{string.Join(", ", tags)}[/][/]");
    }
}
