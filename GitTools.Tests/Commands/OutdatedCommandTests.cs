using System.Diagnostics.CodeAnalysis;
using GitTools.Commands;
using GitTools.Services;
using Spectre.Console.Testing;

namespace GitTools.Tests.Commands;

[ExcludeFromCodeCoverage]
public sealed class OutdatedCommandTests
{
    private readonly IGitRepositoryScanner _scanner = Substitute.For<IGitRepositoryScanner>();
    private readonly IGitService _gitService = Substitute.For<IGitService>();
    private readonly TestConsole _console = new();
    private readonly OutdatedCommand _command;

    public OutdatedCommandTests()
    {
        _console.Interactive();
        _command = new OutdatedCommand(_scanner, _gitService, _console);
    }

    [Fact]
    public void Constructor_ShouldConfigureArgumentsAndOptions()
    {
        _command.Name.ShouldBe("outdated");
        _command.Arguments.Count.ShouldBe(1);
        _command.Arguments[0].Name.ShouldBe("root-directory");
        _command.Options.ShouldContain(o => o.Name == "branch");
        _command.Options.ShouldContain(o => o.Name == "update");
        _command.Options.ShouldContain(o => o.Name == "with-uncommited");
    }

    [Fact]
    public async Task ExecuteAsync_WithUncommittedChanges_ShouldSkipRepo()
    {
        const string ROOT = "/repos";
        const string REPO = "/repos/repo1";
        _scanner.Scan(ROOT).Returns([REPO]);
        _gitService.RunGitCommandAsync(REPO, "status --porcelain").Returns("M file");

        await _command.ExecuteAsync(ROOT, "main", false, false);

        await _gitService.DidNotReceive().RunGitCommandAsync(REPO, "fetch origin");
    }

    [Fact]
    public async Task ExecuteAsync_WithUncommittedChangesAndFlag_ShouldStash()
    {
        const string ROOT = "/repos";
        const string REPO = "/repos/repo1";
        _scanner.Scan(ROOT).Returns([REPO]);
        _gitService.RunGitCommandAsync(REPO, "status --porcelain").Returns("M file");
        _gitService.RunGitCommandAsync(REPO, "fetch origin").Returns("");
        _gitService.RunGitCommandAsync(REPO, "rev-list --count HEAD..origin/main").Returns("1");
        _gitService.RunGitCommandAsync(REPO, "pull origin main").Returns("");

        await _command.ExecuteAsync(ROOT, "main", true, true);

        await _gitService.Received(1).RunGitCommandAsync(REPO, "stash --include-untracked");
        await _gitService.Received(1).RunGitCommandAsync(REPO, "pull origin main");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotAutoUpdate_ShouldPromptSelection()
    {
        const string ROOT = "/repos";
        const string REPO = "/repos/repo1";
        _scanner.Scan(ROOT).Returns([REPO]);
        _gitService.RunGitCommandAsync(REPO, "status --porcelain").Returns("");
        _gitService.RunGitCommandAsync(REPO, "fetch origin").Returns("");
        _gitService.RunGitCommandAsync(REPO, "rev-list --count HEAD..origin/main").Returns("1");
        _gitService.RunGitCommandAsync(REPO, "pull origin main").Returns("");
        _console.Input.PushKey(ConsoleKey.Spacebar);
        _console.Input.PushKey(ConsoleKey.Enter);

        await _command.ExecuteAsync(ROOT, "main", false, false);

        await _gitService.Received(1).RunGitCommandAsync(REPO, "pull origin main");
    }
}
