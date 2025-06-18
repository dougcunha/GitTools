using System.CommandLine;
using System.IO.Abstractions;
using GitTools.Commands;
using GitTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

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
        services.AddSingleton(AnsiConsole.Console);
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IGitRepositoryScanner, GitRepositoryScanner>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<IBackupService, ZipBackupService>();
        services.AddSingleton<TagRemoveCommand>();
        services.AddSingleton<TagListCommand>();
        services.AddSingleton<ReCloneCommand>();
        services.AddSingleton<BulkBackupCommand>();
        services.AddSingleton<BulkRestoreCommand>();

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
    public static RootCommand CreateRootCommand(this IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("GitTools - A tool for searching and removing tags in Git repositories.");

        var tagRemoveCommand = serviceProvider.GetRequiredService<TagRemoveCommand>();
        var tagListCommand = serviceProvider.GetRequiredService<TagListCommand>();
        var recloneCommand = serviceProvider.GetRequiredService<ReCloneCommand>();
        var bulkBackupCommand = serviceProvider.GetRequiredService<BulkBackupCommand>();
        var bulkRestoreCommand = serviceProvider.GetRequiredService<BulkRestoreCommand>();
        rootCommand.AddCommand(tagRemoveCommand);
        rootCommand.AddCommand(tagListCommand);
        rootCommand.AddCommand(recloneCommand);
        rootCommand.AddCommand(bulkBackupCommand);
        rootCommand.AddCommand(bulkRestoreCommand);

        return rootCommand;
    }
}