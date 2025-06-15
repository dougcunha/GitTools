using System.CommandLine;
using System.Diagnostics;
using GitTools.Commands;
using GitTools.Services;
using Spectre.Console.Testing;

namespace GitTools.Tests.Commands;

[ExcludeFromCodeCoverage]
public sealed class ReCloneCommandTests
{
    private readonly MockFileSystem _fileSystem = new();
    private readonly IProcessRunner _runner = Substitute.For<IProcessRunner>();
    private readonly IBackupService _backup = Substitute.For<IBackupService>();
    private readonly TestConsole _console = new();
    private readonly ReCloneCommand _command;

    public ReCloneCommandTests()
    {
        _fileSystem.AddDirectory("/repo/.git");
        _fileSystem.Directory.SetCurrentDirectory("/repo");
        _console.Interactive();
        _command = new ReCloneCommand(_fileSystem, _runner, _backup, _console);
    }

    [Fact]
    public void Constructor_ShouldConfigureOptions()
    {
        _command.Name.ShouldBe("reclone");
        _command.Options.ShouldContain(o => o.Name == "no-backup");
        _command.Options.ShouldContain(o => o.Name == "force");
    }

    [Fact]
    public async Task ExecuteAsync_NotAGitRepository_ShouldShowMessage()
    {
        _fileSystem.Directory.Delete("/repo/.git");
        await _command.ExecuteAsync(false, false);
        _console.Output.ShouldContain("not a git repository");
    }

    [Fact]
    public async Task ExecuteAsync_WithUncommittedChanges_ShouldAbort()
    {
        SetupGitOutputs("M file.txt\n");
        await _command.ExecuteAsync(false, false);
        _console.Output.ShouldContain("Uncommitted changes");
        await _runner.DidNotReceiveWithAnyArgs().RunAsync(null!, null!, null!);
    }

    [Fact]
    public async Task ExecuteAsync_WithForceAndBackup_ShouldCloneAndBackup()
    {
        SetupGitOutputs(string.Empty, "https://example/repo.git\n");
        await _command.ExecuteAsync(false, true);
        await _backup.Received(1).CreateBackup("/repo", "/repo-backup.zip");
    }

    private void SetupGitOutputs(params string[] outputs)
    {
        var call = 0;
        _runner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<DataReceivedEventHandler>(), Arg.Any<DataReceivedEventHandler>())
            .Returns(ci =>
            {
                var outHandler = ci.ArgAt<DataReceivedEventHandler>(1);
                if (call < outputs.Length)
                    outHandler?.Invoke(null!, CreateEvent(outputs[call]));
                call++;
                return 0;
            });
    }

    private static DataReceivedEventArgs CreateEvent(string data)
    {
        var ctor = typeof(DataReceivedEventArgs).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(string)], null)!;
        return (DataReceivedEventArgs)ctor.Invoke([data]);
    }
}
