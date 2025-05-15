using System.CommandLine;

namespace GitTools;

/// <summary>
/// Tool for searching and removing tags in Git repositories and submodules.
/// </summary>
public sealed class Program
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    public static async Task Main(string[] args)
    {
        var rootCommand = new RootCommand("GitTools - A tool for searching and removing tags in Git repositories.");
        
        var dirOption = new Option<string>
        (
            aliases: ["--dir", "-d"],
            description: "Root directory of git repositories"
        )
        {
            IsRequired = true
        };

        var tagsOption = new Option<string[]>
        (
            aliases: ["--tags", "-t"],
            description: "Tags to remove (comma separated or multiple)"
        )
        {
            IsRequired = true
        };

        var remoteOption = new Option<bool>
        (
            aliases: ["--remote", "-r"],
            description: "Also remove the tag from the remote repository (if present)"
        );

        var tagRemoveCommand = new Command("tag-remove", "Remove tags from git repositories")
        {
            dirOption,
            tagsOption,
            remoteOption
        };

        tagRemoveCommand.SetHandler
        (
            async (dir, tags, removeRemote) =>
            {
                var command = new TagRemoveCommand();
                await command.ExecuteAsync(dir, tags, removeRemote);
            },
            dirOption,
            tagsOption,
            remoteOption
        );

        rootCommand.AddCommand(tagRemoveCommand);

        await rootCommand.InvokeAsync(args);
    }
}
