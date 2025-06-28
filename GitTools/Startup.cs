using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO.Abstractions;
using GitTools.Commands;
using GitTools.Models;
using GitTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Spectre.Console;

namespace GitTools;

/// <summary>
/// Startup class for configuring services and commands in the GitTools application.
/// </summary>
public static class Startup
{
    internal static readonly Option<bool> LogAllGitCommandsOption = new("--log-all-git-commands", "-lg")
    {
        Description = "Log all git commands to the console",
        Recursive = true,
    };

    internal static readonly Option<string?> LogFileOption = new("--log-file", "-lf")
    {
        Description = "Path to a log file where console output will be replicated",
        Recursive = true,
        Arity = ArgumentArity.ZeroOrOne
    };

    internal static readonly Option<bool> DisableAnsiOption = new("--disable-ansi", "-da")
    {
        Description = "Disable ANSI color codes in the console output",
        Recursive = true,
    };

    internal static readonly Option<bool> QuietOption = new("--quiet", "-q")
    {
        Description = "Suppress all console output",
        Recursive = true,
    };

    internal static readonly Option<bool> IncludeSubmodulesOption = new("--include-submodules", "-is")
    {
        Description = "Include Git submodules when scanning for repositories",
        Recursive = true,
    };

    internal static readonly Option<string[]> RepositoryFilterOption = new("--repository-filter", "-rf")
    {
        Description = "Filter repositories by name using wildcard patterns (*, ?). Can be specified multiple times.",
        AllowMultipleArgumentsPerToken = true,
        Recursive = true
    };

    /// <summary>
    /// Registers the necessary services for the GitTools application.
    /// </summary>
    /// <param name="services">
    /// The service collection to register services with.
    /// </param>
    /// <returns>
    /// The updated service collection.
    /// </returns>
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        var console = new AnsiConsoleWrapper(AnsiConsole.Console);
        services.AddSingleton(console);
        services.AddSingleton<IAnsiConsole>(console);
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IGitRepositoryScanner, GitRepositoryScanner>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<GitToolsOptions>();
        services.AddSingleton<IBackupService, ZipBackupService>();
        services.AddSingleton<TagRemoveCommand>();
        services.AddSingleton<TagListCommand>();
        services.AddSingleton<ReCloneCommand>();
        services.AddSingleton<BulkBackupCommand>();
        services.AddSingleton<BulkRestoreCommand>();
        services.AddSingleton<SynchronizeCommand>();
        services.AddSingleton<PruneBranchesCommand>();
        services.AddSingleton<ITagSearchService, TagSearchService>();
        services.AddSingleton<ITagValidationService, TagValidationService>();
        services.AddSingleton<IConsoleDisplayService, ConsoleDisplayService>();

        return services;
    }

    /// <summary>
    /// Creates and parses the command line for the GitTools application.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve command dependencies.</param>
    /// <param name="args">The command line arguments to parse.</param>
    /// <returns>A ParseResult containing the parsed command line.</returns>
    public static ParseResult BuildAndParseCommand(this IServiceProvider serviceProvider, params string[] args)
    {
        var rootCommand = BuildRootCommand(serviceProvider);
        var parseResult = CommandLineParser.Parse(rootCommand, args);
        ConfigureGlobalOptions(parseResult, serviceProvider);

        if (parseResult.Errors.Count == 0)
            return parseResult;

        var console = serviceProvider.GetRequiredService<AnsiConsoleWrapper>();
        console.MarkupLine("[red]Error parsing command line arguments:[/]");

        foreach (var error in parseResult.Errors)
            console.MarkupLine($"[red]{error.Message}[/]");

        return parseResult;
    }

    private static RootCommand BuildRootCommand(IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("GitTools - A tool for managing your Git repositories.");

        rootCommand.Options.Add(LogAllGitCommandsOption);
        rootCommand.Options.Add(LogFileOption);
        rootCommand.Options.Add(DisableAnsiOption);
        rootCommand.Options.Add(QuietOption);
        rootCommand.Options.Add(IncludeSubmodulesOption);
        rootCommand.Options.Add(RepositoryFilterOption);

        var tagRemoveCommand = serviceProvider.GetRequiredService<TagRemoveCommand>();
        var tagListCommand = serviceProvider.GetRequiredService<TagListCommand>();
        var recloneCommand = serviceProvider.GetRequiredService<ReCloneCommand>();
        var bulkBackupCommand = serviceProvider.GetRequiredService<BulkBackupCommand>();
        var bulkRestoreCommand = serviceProvider.GetRequiredService<BulkRestoreCommand>();
        var outdatedCommand = serviceProvider.GetRequiredService<SynchronizeCommand>();
        var pruneBranchesCommand = serviceProvider.GetRequiredService<PruneBranchesCommand>();

        rootCommand.Subcommands.Add(tagRemoveCommand);
        rootCommand.Subcommands.Add(tagListCommand);
        rootCommand.Subcommands.Add(recloneCommand);
        rootCommand.Subcommands.Add(bulkBackupCommand);
        rootCommand.Subcommands.Add(bulkRestoreCommand);
        rootCommand.Subcommands.Add(outdatedCommand);
        rootCommand.Subcommands.Add(pruneBranchesCommand);

        return rootCommand;
    }

    private static void ConfigureGlobalOptions(ParseResult parseResult, IServiceProvider serviceProvider)
    {
        var gitToolsOptions = serviceProvider.GetRequiredService<GitToolsOptions>();
        var console = serviceProvider.GetRequiredService<AnsiConsoleWrapper>();

        var logAllGitCommands = parseResult.GetValue(LogAllGitCommandsOption);
        var logFilePath = parseResult.GetValue(LogFileOption);
        var includeSubmodules = parseResult.GetValue(IncludeSubmodulesOption);
        var repositoryFilters = parseResult.GetValue(RepositoryFilterOption) ?? [];
        var disableAnsi = parseResult.GetValue(DisableAnsiOption);
        var quiet = parseResult.GetValue(QuietOption);

        gitToolsOptions.LogAllGitCommands = logAllGitCommands;
        gitToolsOptions.LogFilePath = logFilePath;
        gitToolsOptions.IncludeSubmodules = includeSubmodules;
        gitToolsOptions.RepositoryFilters = repositoryFilters;

        console.Profile.Capabilities.Ansi = !disableAnsi;
        console.Enabled = !quiet;

        if (string.IsNullOrWhiteSpace(gitToolsOptions.LogFilePath))
            return;

        var logger = new LoggerConfiguration()
            .WriteTo.File(gitToolsOptions.LogFilePath)
            .CreateLogger();

        console.SetLogger(logger);
    }
}
