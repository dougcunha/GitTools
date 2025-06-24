using System.CommandLine;
using System.CommandLine.Invocation;
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
    public void BuildCommand_ShouldCreateRootCommandWithCorrectDescription()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand, _) = serviceProvider.BuildCommand();

        // Assert
        rootCommand.ShouldNotBeNull();
        rootCommand.Description.ShouldBe("GitTools - A tool for managing your Git repositories.");
        rootCommand.Options.ShouldContain(static opt => opt.Name == "log-all-git-commands");
    }

    [Fact]
    public void BuildCommand_ShouldReturnInvocationMiddleware()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand, invocationMiddleware) = serviceProvider.BuildCommand();

        // Assert
        invocationMiddleware.ShouldNotBeNull();
        var parse = rootCommand.Parse("--log-all-git-commands");
        invocationMiddleware.Invoke(new InvocationContext(parse), static _ => Task.CompletedTask);
    }

    [Fact]
    public void BuildCommand_ShouldAddTagRemoveCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand, _) = serviceProvider.BuildCommand();

        // Assert
        rootCommand.Subcommands.ShouldContain(static cmd => cmd.Name == "rm");
        var tagRemoveCommand = rootCommand.Subcommands.First(static cmd => cmd.Name == "rm");
        tagRemoveCommand.ShouldBeOfType<TagRemoveCommand>();
    }

    [Fact]
    public void BuildCommand_ShouldAddTagListCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand, _) = serviceProvider.BuildCommand();

        // Assert
        rootCommand.Subcommands.ShouldContain(static cmd => cmd.Name == "ls");
        var tagListCommand = rootCommand.Subcommands.First(static cmd => cmd.Name == "ls");
        tagListCommand.ShouldBeOfType<TagListCommand>();
    }

    [Fact]
    public void BuildCommand_ShouldAddRecloneCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand, _) = serviceProvider.BuildCommand();

        // Assert
        rootCommand.Subcommands.ShouldContain(static cmd => cmd.Name == "reclone");
        var recloneCommand = rootCommand.Subcommands.First(static cmd => cmd.Name == "reclone");
        recloneCommand.ShouldBeOfType<ReCloneCommand>();
    }

    [Fact]
    public void BuildCommand_ShouldAddBulkBackupCommand()
    {
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        var (_, rootCommand, _) = provider.BuildCommand();

        rootCommand.Subcommands.ShouldContain(static c => c.Name == "bkp");
        var cmd = rootCommand.Subcommands.First(static c => c.Name == "bkp");
        cmd.ShouldBeOfType<BulkBackupCommand>();
    }

    [Fact]
    public void BuildCommand_ShouldAddBulkRestoreCommand()
    {
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        var (_, rootCommand, _) = provider.BuildCommand();

        rootCommand.Subcommands.ShouldContain(static c => c.Name == "restore");
        var cmd = rootCommand.Subcommands.First(static c => c.Name == "restore");
        cmd.ShouldBeOfType<BulkRestoreCommand>();
    }

    [Fact]
    public void BuildCommand_ShouldAddOutdatedCommand()
    {
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        var (_, rootCommand, _) = provider.BuildCommand();

        rootCommand.Subcommands.ShouldContain(static c => c.Name == "sync");
        var cmd = rootCommand.Subcommands.First(static c => c.Name == "sync");
        cmd.ShouldBeOfType<SynchronizeCommand>();
    }

    [Fact]
    public void BuildCommand_ShouldResolveTagRemoveCommandFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand, _) = serviceProvider.BuildCommand();
        var tagRemoveCommand = rootCommand.Subcommands.OfType<TagRemoveCommand>().First();

        // Assert
        tagRemoveCommand.ShouldNotBeNull();
        tagRemoveCommand.ShouldBeOfType<TagRemoveCommand>();
    }

    [Fact]
    public void BuildCommand_ShouldResolveTagListCommandFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand, _) = serviceProvider.BuildCommand();
        var tagListCommand = rootCommand.Subcommands.OfType<TagListCommand>().First();

        // Assert
        tagListCommand.ShouldNotBeNull();
        tagListCommand.ShouldBeOfType<TagListCommand>();
    }

    [Fact]
    public void BuildCommand_ShouldResolveBulkBackupCommandFromServiceProvider()
    {
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        var (_, rootCommand, _) = provider.BuildCommand();
        var cmd = rootCommand.Subcommands.OfType<BulkBackupCommand>().First();

        cmd.ShouldNotBeNull();
        cmd.ShouldBeOfType<BulkBackupCommand>();
    }

    [Fact]
    public void BuildCommand_ShouldResolveBulkRestoreCommandFromServiceProvider()
    {
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        var (_, rootCommand, _) = provider.BuildCommand();
        var cmd = rootCommand.Subcommands.OfType<BulkRestoreCommand>().First();

        cmd.ShouldNotBeNull();
        cmd.ShouldBeOfType<BulkRestoreCommand>();
    }

    [Fact]
    public void BuildCommand_ShouldResolveOutdatedCommandFromServiceProvider()
    {
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        var (_, rootCommand, _) = provider.BuildCommand();
        var cmd = rootCommand.Subcommands.OfType<SynchronizeCommand>().First();

        cmd.ShouldNotBeNull();
        cmd.ShouldBeOfType<SynchronizeCommand>();
    }

    [Fact]
    public void BuildCommand_WithMissingTagRemoveCommand_ShouldThrowException()
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
        Should.Throw<InvalidOperationException>(() => serviceProvider.BuildCommand());
    }

    [Fact]
    public void BuildCommand_ShouldCreateNewInstanceEachTime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand1, _) = serviceProvider.BuildCommand();
        var (_, rootCommand2, _) = serviceProvider.BuildCommand();

        // Assert
        rootCommand1.ShouldNotBeSameAs(rootCommand2);
    }

    [Fact]
    public void BuildCommand_ShouldReuseTagRemoveCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand1, _) = serviceProvider.BuildCommand();
        var (_, rootCommand2, _) = serviceProvider.BuildCommand();

        var tagRemoveCommand1 = rootCommand1.Subcommands.OfType<TagRemoveCommand>().First();
        var tagRemoveCommand2 = rootCommand2.Subcommands.OfType<TagRemoveCommand>().First();

        // Assert
        tagRemoveCommand1.ShouldBeSameAs(tagRemoveCommand2);
    }

    [Fact]
    public void BuildCommand_ShouldReuseTagListCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand1, _) = serviceProvider.BuildCommand();
        var (_, rootCommand2, _) = serviceProvider.BuildCommand();

        var tagListCommand1 = rootCommand1.Subcommands.OfType<TagListCommand>().First();
        var tagListCommand2 = rootCommand2.Subcommands.OfType<TagListCommand>().First();

        // Assert
        tagListCommand1.ShouldBeSameAs(tagListCommand2);
    }

    [Fact]
    public void BuildCommand_ShouldReuseBulkBackupCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand1, _) = provider.BuildCommand();
        var (_, rootCommand2, _) = provider.BuildCommand();

        var c1 = rootCommand1.Subcommands.OfType<BulkBackupCommand>().First();
        var c2 = rootCommand2.Subcommands.OfType<BulkBackupCommand>().First();

        // Assert
        c1.ShouldBeSameAs(c2);
    }

    [Fact]
    public void BuildCommand_ShouldReuseBulkRestoreCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand1, _) = provider.BuildCommand();
        var (_, rootCommand2, _) = provider.BuildCommand();

        var c1 = rootCommand1.Subcommands.OfType<BulkRestoreCommand>().First();
        var c2 = rootCommand2.Subcommands.OfType<BulkRestoreCommand>().First();

        // Assert
        c1.ShouldBeSameAs(c2);
    }

    [Fact]
    public void BuildCommand_ShouldReuseOutdatedCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var provider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand1, _) = provider.BuildCommand();
        var (_, rootCommand2, _) = provider.BuildCommand();

        var c1 = rootCommand1.Subcommands.OfType<SynchronizeCommand>().First();
        var c2 = rootCommand2.Subcommands.OfType<SynchronizeCommand>().First();

        // Assert
        c1.ShouldBeSameAs(c2);
    }

    [Fact]
    public void BuildCommand_ShouldRegisterGlobalOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var (_, rootCommand, _) = serviceProvider.BuildCommand();

        // Assert
        rootCommand.Options.ShouldContain(static opt => opt.Name == "log-all-git-commands");
        rootCommand.Options.ShouldContain(static opt => opt.Name == "disable-ansi");
        rootCommand.Options.ShouldContain(static opt => opt.Name == "quiet");
        rootCommand.Options.ShouldContain(static opt => opt.Name == "help");
        rootCommand.Options.ShouldContain(static opt => opt.Name == "version");
    }
}
