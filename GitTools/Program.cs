using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using GitTools;
using GitTools.Models;
using Microsoft.Extensions.DependencyInjection;

var serviceProvider = new ServiceCollection()
    .RegisterServices()
    .BuildServiceProvider();

var rootCommand = serviceProvider.CreateRootCommand();
var logAllGitCommandsOption = new Option<bool>(["--log-all-git-commands", "-lg"], "Log all git commands to the console");
rootCommand.AddGlobalOption(logAllGitCommandsOption);
var opts = serviceProvider.GetRequiredService<GitToolsOptions>();

var builder = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .AddMiddleware(async (context, next) =>
    {
        opts.LogAllGitCommands = context.ParseResult.GetValueForOption(logAllGitCommandsOption);
        await next(context);
    });

await builder.Build().InvokeAsync(args).ConfigureAwait(false);

if (Debugger.IsAttached)
{
    Console.WriteLine("Press any key to exit...");
    Console.Read();
}
