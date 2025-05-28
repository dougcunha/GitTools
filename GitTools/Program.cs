using System.CommandLine;
using System.Diagnostics;
using GitTools;
using Microsoft.Extensions.DependencyInjection;

var rootCommand = new ServiceCollection()
    .RegisterServices()
    .BuildServiceProvider()
    .CreateRootCommand();

await rootCommand.InvokeAsync(args);

if (Debugger.IsAttached)
{
    Console.WriteLine("Press any key to exit...");
    Console.Read();
}
