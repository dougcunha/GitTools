using System.CommandLine;
using System.IO.Abstractions;
using GitTools.Commands;
using GitTools.Services;
using GitTools.Models;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Spectre.Console;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.CommandLine.Invocation;

namespace GitTools;

/// <summary>
/// Startup class for configuring services and commands in the GitTools application.
/// </summary>
public static class Startup
{
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
        services.AddSingleton<ITagSearchService, TagSearchService>();
        services.AddSingleton<ITagValidationService, TagValidationService>();
        services.AddSingleton<IConsoleDisplayService, ConsoleDisplayService>();

        return services;
    }

    /// <summary>
    /// Creates the root command for the GitTools application using the provided service provider.
    /// This command serves as the entry point for the application and includes all registered commands.
    /// </summary>
    /// <param name="serviceProvider">
    /// The service provider to resolve command dependencies.
    /// </param>
    /// <returns>
    /// The root command for the GitTools application.
    /// </returns>
    public static (Parser parser, RootCommand rootCommand, InvocationMiddleware parseOptionsMiddleware) BuildCommand(this IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("GitTools - A tool for managing your Git repositories.");
        var logAllGitCommandsOption = new Option<bool>(["--log-all-git-commands", "-lg"], "Log all git commands to the console");
        var logFileOption = new Option<string?>(["--log-file", "-lf"], "Path to a log file where console output will be replicated");
        var disableAnsiOption = new Option<bool>(["--disable-ansi", "-da"], "Disable ANSI color codes in the console output");
        var quietOption = new Option<bool>(["--quiet", "-q"], "Suppress all console output");
        var includeSubmodulesOption = new Option<bool>(["--include-submodules", "-is"], () => true, "Include Git submodules when scanning for repositories");
        rootCommand.AddGlobalOption(logAllGitCommandsOption);
        rootCommand.AddGlobalOption(logFileOption);
        rootCommand.AddGlobalOption(disableAnsiOption);
        rootCommand.AddGlobalOption(quietOption);
        rootCommand.AddGlobalOption(includeSubmodulesOption);
        var tagRemoveCommand = serviceProvider.GetRequiredService<TagRemoveCommand>();
        var tagListCommand = serviceProvider.GetRequiredService<TagListCommand>();
        var recloneCommand = serviceProvider.GetRequiredService<ReCloneCommand>();
        var bulkBackupCommand = serviceProvider.GetRequiredService<BulkBackupCommand>();
        var bulkRestoreCommand = serviceProvider.GetRequiredService<BulkRestoreCommand>();
        var outdatedCommand = serviceProvider.GetRequiredService<SynchronizeCommand>();
        var gitToolsOptions = serviceProvider.GetRequiredService<GitToolsOptions>();
        var console = serviceProvider.GetRequiredService<AnsiConsoleWrapper>();

        rootCommand.AddCommand(tagRemoveCommand);
        rootCommand.AddCommand(tagListCommand);
        rootCommand.AddCommand(recloneCommand);
        rootCommand.AddCommand(bulkBackupCommand);
        rootCommand.AddCommand(bulkRestoreCommand);
        rootCommand.AddCommand(outdatedCommand);

        var builder = new CommandLineBuilder(rootCommand);

        var parser = builder
            .UseDefaults()
            .AddMiddleware(ParseGlobalOptions).Build();

        return (parser, rootCommand, ParseGlobalOptions);

        Task ParseGlobalOptions(InvocationContext context, Func<InvocationContext, Task> next)
        {
            gitToolsOptions.LogAllGitCommands = context.ParseResult.GetValueForOption(logAllGitCommandsOption);
            gitToolsOptions.LogFilePath = context.ParseResult.GetValueForOption(logFileOption);
            var disableAnsi = context.ParseResult.GetValueForOption(disableAnsiOption);
            var quiet = context.ParseResult.GetValueForOption(quietOption);
            gitToolsOptions.IncludeSubmodules = context.ParseResult.GetValueForOption(includeSubmodulesOption);
            console.Profile.Capabilities.Ansi = !disableAnsi;
            console.Enabled = !quiet;

            if (string.IsNullOrWhiteSpace(gitToolsOptions.LogFilePath))
                return next(context);

            var logger = new LoggerConfiguration()
                .WriteTo.File(gitToolsOptions.LogFilePath)
                .CreateLogger();

            console.SetLogger(logger);

            return next(context);
        }
    }
}
