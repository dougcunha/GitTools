using System.CommandLine;
using System.IO.Abstractions;
using GitTools.Commands;
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
        serviceProvider.GetService<TagRemoveCommand>().ShouldNotBeNull();
    }

    [Fact]
    public void RegisterServices_ShouldRegisterServicesWithCorrectLifetime()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.RegisterServices();

        // Assert
        var ansiConsoleDescriptor = services.First(s => s.ServiceType == typeof(IAnsiConsole));
        var processRunnerDescriptor = services.First(s => s.ServiceType == typeof(IProcessRunner));
        var gitScannerDescriptor = services.First(s => s.ServiceType == typeof(IGitRepositoryScanner));
        var fileSystemDescriptor = services.First(s => s.ServiceType == typeof(IFileSystem));
        var gitServiceDescriptor = services.First(s => s.ServiceType == typeof(IGitService));
        var tagRemoveCommandDescriptor = services.First(s => s.ServiceType == typeof(TagRemoveCommand));

        ansiConsoleDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        processRunnerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        gitScannerDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        fileSystemDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        gitServiceDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        tagRemoveCommandDescriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void RegisterServices_ShouldRegisterServicesWithCorrectImplementations()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.RegisterServices();

        // Assert
        var processRunnerDescriptor = services.First(s => s.ServiceType == typeof(IProcessRunner));
        var gitScannerDescriptor = services.First(s => s.ServiceType == typeof(IGitRepositoryScanner));
        var fileSystemDescriptor = services.First(s => s.ServiceType == typeof(IFileSystem));
        var gitServiceDescriptor = services.First(s => s.ServiceType == typeof(IGitService));

        processRunnerDescriptor.ImplementationType.ShouldBe(typeof(ProcessRunner));
        gitScannerDescriptor.ImplementationType.ShouldBe(typeof(GitRepositoryScanner));
        fileSystemDescriptor.ImplementationType.ShouldBe(typeof(FileSystem));
        gitServiceDescriptor.ImplementationType.ShouldBe(typeof(GitService));
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

        console1.ShouldBeSameAs(console2);
        console1.ShouldBeSameAs(AnsiConsole.Console);
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
    public void CreateRootCommand_ShouldCreateRootCommandWithCorrectDescription()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand = serviceProvider.CreateRootCommand();

        // Assert
        rootCommand.ShouldNotBeNull();
        rootCommand.Description.ShouldBe("GitTools - A tool for searching and removing tags in Git repositories.");
    }

    [Fact]
    public void CreateRootCommand_ShouldAddTagRemoveCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand = serviceProvider.CreateRootCommand();

        // Assert
        rootCommand.Subcommands.ShouldContain(cmd => cmd.Name == "rm");
        var tagRemoveCommand = rootCommand.Subcommands.First(cmd => cmd.Name == "rm");
        tagRemoveCommand.ShouldBeOfType<TagRemoveCommand>();
    }

    [Fact]
    public void CreateRootCommand_ShouldResolveTagRemoveCommandFromServiceProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand = serviceProvider.CreateRootCommand();
        var tagRemoveCommand = rootCommand.Subcommands.OfType<TagRemoveCommand>().First();

        // Assert
        tagRemoveCommand.ShouldNotBeNull();
        tagRemoveCommand.ShouldBeOfType<TagRemoveCommand>();
    }

    [Fact]
    public void CreateRootCommand_WithMissingTagRemoveCommand_ShouldThrowException()
    {
        // Arrange
        var services = new ServiceCollection();
        // Intentionally not registering TagRemoveCommand
        services.AddSingleton(AnsiConsole.Console);
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IGitRepositoryScanner, GitRepositoryScanner>();
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IGitService, GitService>();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => serviceProvider.CreateRootCommand());
    }

    [Fact]
    public void CreateRootCommand_ShouldCreateNewInstanceEachTime()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand1 = serviceProvider.CreateRootCommand();
        var rootCommand2 = serviceProvider.CreateRootCommand();

        // Assert
        rootCommand1.ShouldNotBeSameAs(rootCommand2);
    }

    [Fact]
    public void CreateRootCommand_ShouldReuseTagRemoveCommandInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.RegisterServices();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var rootCommand1 = serviceProvider.CreateRootCommand();
        var rootCommand2 = serviceProvider.CreateRootCommand();

        var tagRemoveCommand1 = rootCommand1.Subcommands.OfType<TagRemoveCommand>().First();
        var tagRemoveCommand2 = rootCommand2.Subcommands.OfType<TagRemoveCommand>().First();

        // Assert
        tagRemoveCommand1.ShouldBeSameAs(tagRemoveCommand2);
    }
}
