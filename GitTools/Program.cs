using System.CommandLine.Parsing;
using System.Diagnostics;
using GitTools;
using Microsoft.Extensions.DependencyInjection;

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
