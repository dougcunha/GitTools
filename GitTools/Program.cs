using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace GitTools;

[ExcludeFromCodeCoverage]
file static class Program
{
    private static async Task Main(string[] args)
    {
        var (parser, _, _) = new ServiceCollection()
            .RegisterServices()
            .BuildServiceProvider()
            .BuildCommand();

        await parser.InvokeAsync(args).ConfigureAwait(false);

        if (Debugger.IsAttached)
        {
            Console.WriteLine("Press any key to exit...");
            Console.Read();
        }
    }
}