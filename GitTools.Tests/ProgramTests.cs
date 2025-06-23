using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using GitTools.Models;
using Microsoft.Extensions.DependencyInjection;

namespace GitTools.Tests;

[ExcludeFromCodeCoverage]
public sealed class ProgramTests
{
    [Fact]
    public async Task InvokeAsync_WithLogAllGitCommandsOption_ShouldSetOption()
    {
        // Arrange
        var provider = new ServiceCollection().RegisterServices().BuildServiceProvider();
        var root = provider.CreateRootCommand();
        var option = new Option<bool>(["--log-all-git-commands", "-lg"]);
        root.AddGlobalOption(option);

        var gitOptions = provider.GetRequiredService<GitToolsOptions>();

        var builder = new CommandLineBuilder(root)
            .UseDefaults()
            .AddMiddleware(async (context, next) =>
            {
                gitOptions.LogAllGitCommands = context.ParseResult.GetValueForOption(option);
                await next(context);
            });

        // Act
        await builder.Build().InvokeAsync(["--log-all-git-commands", "--help"]);

        // Assert
        gitOptions.LogAllGitCommands.ShouldBeTrue();
    }
}
