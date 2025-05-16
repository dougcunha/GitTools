using System.CommandLine;
using System.Diagnostics;
using GitTools.Commands;

var rootCommand = new RootCommand("GitTools - A tool for searching and removing tags in Git repositories.");
rootCommand.AddCommand(new TagRemoveCommand());
await rootCommand.InvokeAsync(args);

if (Debugger.IsAttached)
{
    Console.WriteLine("Press any key to exit...");
    Console.Read();
}
