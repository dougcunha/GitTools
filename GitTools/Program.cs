using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

[assembly: InternalsVisibleTo("GitTools.Tests")]

namespace GitTools;

[ExcludeFromCodeCoverage]
file static class Program
{
    private static async Task Main(string[] args)
    {
        var serviceProvider = new ServiceCollection()
            .RegisterServices()
            .BuildServiceProvider();

        var parseResult = serviceProvider.BuildAndParseCommand(args);
        var exitCode = await parseResult.InvokeAsync().ConfigureAwait(false);

        if (Debugger.IsAttached)
        {
            Console.WriteLine("Press any key to exit...");
            Console.Read();
        }

        Environment.Exit(exitCode);
    }
}