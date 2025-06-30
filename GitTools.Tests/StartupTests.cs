using System.CommandLine;
using System.IO.Abstractions;
using GitTools.Commands;
using GitTools.Models;
using GitTools.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace GitTools.Tests;

[ExcludeFromCodeCoverage]
public sealed class StartupTests
{
    private static RootCommand BuildRootCommand(IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("GitTools - A tool for managing your Git repositories.");

        rootCommand.Options.Add(Startup.LogAllGitCommandsOption);
        rootCommand.Options.Add(Startup.LogFileOption);
        rootCommand.Options.Add(Startup.DisableAnsiOption);
        rootCommand.Options.Add(Startup.QuietOption);
        rootCommand.Options.Add(Startup.IncludeSubmodulesOption);
        rootCommand.Options.Add(Startup.RepositoryFilterOption);

        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<TagRemoveCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<TagListCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<ReCloneCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<BulkBackupCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<BulkRestoreCommand>());
        rootCommand.Subcommands.Add(serviceProvider.GetRequiredService<SynchronizeCommand>());

        return rootCommand;
    }

    [Fact]
    public void RegisterServices_ShouldRegisterAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.RegisterServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();

        // Verify all services are registered
        serviceProvider.GetService<IAnsiConsole>().ShouldNotBeNull();
        serviceProvider.GetService<IProcessRunner>().ShouldNotBeNull();
        serviceProvider.GetService<IGitRepositoryScanner>().ShouldNotBeNull();
        serviceProvider.GetService<IFileSystem>().ShouldNotBeNull();
        serviceProvider.GetService<IGitService>().ShouldNotBeNull();
        serviceProvider.GetService<GitToolsOptions>().ShouldNotBeNull();
        serviceProvider.GetService<TagRemoveCommand>().ShouldNotBeNull();
        serviceProvider.GetService<TagListCommand>().ShouldNotBeNull();
        serviceProvider.GetService<BulkBackupCommand>().ShouldNotBeNull();
        serviceProvider.GetService<BulkRestoreCommand>().ShouldNotBeNull();
        serviceProvider.GetService<SynchronizeCommand>().ShouldNotBeNull();
    }

    [Fact]
    public void RegisterServices_ShouldRegisterServicesWithCorrectLifetime()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.RegisterServices();

        // Assert
        var ansiConsoleDescriptor = services.First(static s => s.ServiceType == typeof(IAnsiConsole));
        var processRunnerDescriptor = services.First(static s => s.ServiceType == typeof(IProcessRunner));
        var gitScannerDescriptor = services.First(static s => s.ServiceType == typeof(IGitRepositoryScanner));
        var fileSystemDescriptor = services.First(static s => s.ServiceType == typeof(IFileSystem));
        var gitServiceDescriptor = services.First(static s => s.ServiceType == typeof(IGitService));
        var tagRemoveCommandDescriptor = services.First(static s => s.ServiceType == typeof(TagRemoveCommand));
        var tagListCommandDescriptor = services.First(static s => s.ServiceType == typeof(TagListCommand));
        var bulkBackupDescriptor = services.First(static s => s.ServiceType == typeof(BulkBackupCommand));
        var bulkRestoreDescriptor = services.First(static s => s.ServiceType == typeof(BulkRestoreCommand));
        var outdatedDescriptor = services.First(static s => s.ServiceType == typeof(SynchronizeCommand));
        var optionsDescriptor = services.First(static s => s.ServiceType == typeof(GitToolsOptions));

        ansiConsoleDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        processRunnerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        gitScannerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        fileSystemDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        gitServiceDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        tagRemoveCommandDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        tagListCommandDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        bulkBackupDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        bulkRestoreDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        outdatedDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        optionsDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void RegisterServices_ShouldRegisterServicesWithCorrectImplementations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.RegisterServices();

        // Assert
        var processRunnerDescriptor = services.First(static s => s.ServiceType == typeof(IProcessRunner));
        var gitScannerDescriptor = services.First(static s => s.ServiceType == typeof(IGitRepositoryScanner));
        var fileSystemDescriptor = services.First(static s => s.ServiceType == typeof(IFileSystem));
        var gitServiceDescriptor = services.First(static s => s.ServiceType == typeof(IGitService));
        var backupServiceDescriptor = services.First(static s => s.ServiceType == typeof(IBackupService));
        var bulkBackupDescriptor = services.First(static s => s.ServiceType == typeof(BulkBackupCommand));
        var bulkRestoreDescriptor = services.First(static s => s.ServiceType == typeof(BulkRestoreCommand));
        var outdatedDescriptor = services.First(static s => s.ServiceType == typeof(SynchronizeCommand));
        var optionsDescriptor = services.First(static s => s.ServiceType == typeof(GitToolsOptions));

        processRunnerDescriptor.ImplementationType.ShouldBe(typeof(ProcessRunner));
        gitScannerDescriptor.ImplementationType.ShouldBe(typeof(GitRepositoryScanner));
        fileSystemDescriptor.ImplementationType.ShouldBe(typeof(FileSystem));
        gitServiceDescriptor.ImplementationType.ShouldBe(typeof(GitService));
        backupServiceDescriptor.ImplementationType.ShouldBe(typeof(ZipBackupService));
        bulkBackupDescriptor.ImplementationType.ShouldBe(typeof(BulkBackupCommand));
        bulkRestoreDescriptor.ImplementationType.ShouldBe(typeof(BulkRestoreCommand));
        outdatedDescriptor.ImplementationType.ShouldBe(typeof(SynchronizeCommand));
        optionsDescriptor.ImplementationType.ShouldBe(typeof(GitToolsOptions));
    }

    [Fact]
    public void RegisterServices_ShouldRegisterAnsiConsoleAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.RegisterServices();

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var console1 = serviceProvider.GetService<IAnsiConsole>();
        var console2 = serviceProvider.GetService<IAnsiConsole>();
        var consoleWrapper = serviceProvider.GetService<AnsiConsoleWrapper>();

        console1.ShouldBeSameAs(console2);
        console1.ShouldBeSameAs(consoleWrapper);
    }

    [Fact]
    public void RegisterServices_ShouldAllowFluentChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.RegisterServices();

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void BuildAndParseCommand_ShouldCreateRootCommandWithCorrectDescription()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var parseResult = serviceProvider.BuildAndParseCommand("--help");

        // Assert
        parseResult.ShouldNotBeNull();
        parseResult.CommandResult.Command.ShouldNotBeNull();
        parseResult.CommandResult.Command.Description.ShouldBe("GitTools - A tool for managing your Git repositories.");
    }

    [Fact]
    public void BuildAndParseCommand_WithLogFile_ShouldConfigureLogger()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var parseResult = serviceProvider.BuildAndParseCommand("sync", "projetos", "--log-file", "log.txt");

        // Assert
        parseResult.ShouldNotBeNull();
        parseResult.Errors.ShouldBeEmpty();
        var console = serviceProvider.GetService<AnsiConsoleWrapper>();
        console.ShouldNotBeNull();
        console.IsLogging.ShouldBeTrue();
        var gitToolsOptions = serviceProvider.GetService<GitToolsOptions>();
        gitToolsOptions.ShouldNotBeNull();
        gitToolsOptions.LogFilePath.ShouldBe("log.txt");
    }

    [Fact]
    public void BuildAndParseCommand_WithIncludeSubmodulesFalse_ShouldUpdateOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var parseResult = provider.BuildAndParseCommand("sync", "projetos", "--include-submodules", "false");

        // Assert
        parseResult.ShouldNotBeNull();
        parseResult.Errors.ShouldBeEmpty();
        var options = provider.GetRequiredService<GitToolsOptions>();
        options.IncludeSubmodules.ShouldBeFalse();
    }

    [Fact]
    public void BuildAndParseCommand_WithRepositoryFilter_ShouldUpdateOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var parseResult = provider.BuildAndParseCommand("bkp", "projetos", "bkp.json", "--repository-filter", "*-api", "--repository-filter", "frontend-*");

        // Assert
        parseResult.ShouldNotBeNull();
        parseResult.Errors.ShouldBeEmpty();
        var options = provider.GetRequiredService<GitToolsOptions>();
        options.RepositoryFilters.ShouldBe(["*-api", "frontend-*"]);
    }

    [Fact]
    public void BuildAndParseCommand_WithError_ShouldPrintErrorMessages()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var parseResult = serviceProvider.BuildAndParseCommand("invalid-command");

        // Assert
        parseResult.ShouldNotBeNull();
        parseResult.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void BuildRootCommand_ShouldAddTagRemoveCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(serviceProvider);

        // Assert
        rootCommand.Subcommands.ShouldContain(static cmd => cmd.Name == "rm");
        var tagRemoveCommand = rootCommand.Subcommands.First(static cmd => cmd.Name == "rm");
        tagRemoveCommand.ShouldBeOfType<TagRemoveCommand>();
    }

    [Fact]
    public void BuildRootCommand_ShouldAddTagListCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(serviceProvider);

        // Assert
        rootCommand.Subcommands.ShouldContain(static cmd => cmd.Name == "ls");
        var tagListCommand = rootCommand.Subcommands.First(static cmd => cmd.Name == "ls");
        tagListCommand.ShouldBeOfType<TagListCommand>();
    }

    [Fact]
    public void BuildRootCommand_ShouldAddRecloneCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(serviceProvider);

        // Assert
        rootCommand.Subcommands.ShouldContain(static cmd => cmd.Name == "reclone");
        var recloneCommand = rootCommand.Subcommands.First(static cmd => cmd.Name == "reclone");
        recloneCommand.ShouldBeOfType<ReCloneCommand>();
    }

    [Fact]
    public void BuildRootCommand_ShouldAddBulkBackupCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(provider);

        // Assert
        rootCommand.Subcommands.ShouldContain(static c => c.Name == "bkp");
        var cmd = rootCommand.Subcommands.First(static c => c.Name == "bkp");
        cmd.ShouldBeOfType<BulkBackupCommand>();
    }

    [Fact]
    public void BuildRootCommand_ShouldAddBulkRestoreCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(provider);

        // Assert
        rootCommand.Subcommands.ShouldContain(static c => c.Name == "restore");
        var cmd = rootCommand.Subcommands.First(static c => c.Name == "restore");
        cmd.ShouldBeOfType<BulkRestoreCommand>();
    }

    [Fact]
    public void BuildRootCommand_ShouldAddOutdatedCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(provider);

        // Assert
        rootCommand.Subcommands.ShouldContain(static c => c.Name == "sync");
        var cmd = rootCommand.Subcommands.First(static c => c.Name == "sync");
        cmd.ShouldBeOfType<SynchronizeCommand>();
    }

    [Fact]
    public void BuildRootCommand_ShouldResolveTagRemoveCommandFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(serviceProvider);
        var tagRemoveCommand = rootCommand.Subcommands.OfType<TagRemoveCommand>().First();

        // Assert
        tagRemoveCommand.ShouldNotBeNull();
        tagRemoveCommand.ShouldBeOfType<TagRemoveCommand>();
    }

    [Fact]
    public void BuildRootCommand_ShouldResolveTagListCommandFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(serviceProvider);
        var tagListCommand = rootCommand.Subcommands.OfType<TagListCommand>().First();

        // Assert
        tagListCommand.ShouldNotBeNull();
        tagListCommand.ShouldBeOfType<TagListCommand>();
    }

    [Fact]
    public void BuildRootCommand_ShouldResolveBulkBackupCommandFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(provider);
        var cmd = rootCommand.Subcommands.OfType<BulkBackupCommand>().First();

        // Assert
        cmd.ShouldNotBeNull();
        cmd.ShouldBeOfType<BulkBackupCommand>();
    }

    [Fact]
    public void BuildRootCommand_ShouldResolveBulkRestoreCommandFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(provider);
        var cmd = rootCommand.Subcommands.OfType<BulkRestoreCommand>().First();

        // Assert
        cmd.ShouldNotBeNull();
        cmd.ShouldBeOfType<BulkRestoreCommand>();
    }

    [Fact]
    public void BuildRootCommand_ShouldResolveOutdatedCommandFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(provider);
        var cmd = rootCommand.Subcommands.OfType<SynchronizeCommand>().First();

        // Assert
        cmd.ShouldNotBeNull();
        cmd.ShouldBeOfType<SynchronizeCommand>();
    }

    [Fact]
    public void BuildRootCommand_WithMissingTagRemoveCommand_ShouldThrowException()
    {
        // Arrange
        var services = new ServiceCollection();
        // Intentionally not registering TagRemoveCommand
        services.AddSingleton(AnsiConsole.Console);
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IGitRepositoryScanner, GitRepositoryScanner>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<TagListCommand>();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => BuildRootCommand(serviceProvider));
    }

    [Fact]
    public void BuildRootCommand_ShouldCreateNewInstanceEachTime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand1 = BuildRootCommand(serviceProvider);
        var rootCommand2 = BuildRootCommand(serviceProvider);

        // Assert
        rootCommand1.ShouldNotBeSameAs(rootCommand2);
    }

    [Fact]
    public void BuildRootCommand_ShouldReuseTagRemoveCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand1 = BuildRootCommand(serviceProvider);
        var rootCommand2 = BuildRootCommand(serviceProvider);

        var tagRemoveCommand1 = rootCommand1.Subcommands.OfType<TagRemoveCommand>().First();
        var tagRemoveCommand2 = rootCommand2.Subcommands.OfType<TagRemoveCommand>().First();

        // Assert
        tagRemoveCommand1.ShouldBeSameAs(tagRemoveCommand2);
    }

    [Fact]
    public void BuildRootCommand_ShouldReuseTagListCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand1 = BuildRootCommand(serviceProvider);
        var rootCommand2 = BuildRootCommand(serviceProvider);

        var tagListCommand1 = rootCommand1.Subcommands.OfType<TagListCommand>().First();
        var tagListCommand2 = rootCommand2.Subcommands.OfType<TagListCommand>().First();

        // Assert
        tagListCommand1.ShouldBeSameAs(tagListCommand2);
    }

    [Fact]
    public void BuildRootCommand_ShouldReuseBulkBackupCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var rootCommand1 = BuildRootCommand(provider);
        var rootCommand2 = BuildRootCommand(provider);

        var c1 = rootCommand1.Subcommands.OfType<BulkBackupCommand>().First();
        var c2 = rootCommand2.Subcommands.OfType<BulkBackupCommand>().First();

        // Assert
        c1.ShouldBeSameAs(c2);
    }

    [Fact]
    public void BuildRootCommand_ShouldReuseBulkRestoreCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var rootCommand1 = BuildRootCommand(provider);
        var rootCommand2 = BuildRootCommand(provider);

        var c1 = rootCommand1.Subcommands.OfType<BulkRestoreCommand>().First();
        var c2 = rootCommand2.Subcommands.OfType<BulkRestoreCommand>().First();

        // Assert
        c1.ShouldBeSameAs(c2);
    }

    [Fact]
    public void BuildRootCommand_ShouldReuseOutdatedCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var rootCommand1 = BuildRootCommand(provider);
        var rootCommand2 = BuildRootCommand(provider);

        var c1 = rootCommand1.Subcommands.OfType<SynchronizeCommand>().First();
        var c2 = rootCommand2.Subcommands.OfType<SynchronizeCommand>().First();

        // Assert
        c1.ShouldBeSameAs(c2);
    }

    [Fact]
    public void BuildRootCommand_ShouldRegisterGlobalOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand = BuildRootCommand(serviceProvider);

        // Assert
        rootCommand.Options.ShouldContain(static opt => opt.Name == "--log-all-git-commands");
        rootCommand.Options.ShouldContain(static opt => opt.Name == "--log-file");
        rootCommand.Options.ShouldContain(static opt => opt.Name == "--disable-ansi");
        rootCommand.Options.ShouldContain(static opt => opt.Name == "--quiet");
        rootCommand.Options.ShouldContain(static opt => opt.Name == "--include-submodules");
        rootCommand.Options.ShouldContain(static opt => opt.Name == "--repository-filter");
    }
}
