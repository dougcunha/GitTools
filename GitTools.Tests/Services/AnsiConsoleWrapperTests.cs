using Spectre.Console;
using Spectre.Console.Rendering;
using GitTools.Services;
using Spectre.Console.Testing;
using Serilog;

namespace GitTools.Tests.Services;

/// <summary>
/// Tests for <see cref="AnsiConsoleWrapper"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class AnsiConsoleWrapperTests
{
    private const bool ENABLED = true;
    private const bool DISABLED = false;

    private readonly IAnsiConsole _mockConsole = Substitute.For<IAnsiConsole>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly AnsiConsoleWrapper _wrapper;

    public AnsiConsoleWrapperTests()
    {
        _wrapper = new AnsiConsoleWrapper(_mockConsole);
        _wrapper.SetLogger(_logger);
    }

    [Fact]
    public void Clear_WhenEnabled_DelegatesToInnerConsole()
    {
        // Arrange
        _wrapper.Enabled = ENABLED;

        // Act
        _wrapper.Clear(true);

        // Assert
        _mockConsole.Received(1).Clear(true);
    }

    [Fact]
    public void Clear_WhenDisabled_DoesNotDelegate()
    {
        // Arrange
        _wrapper.Enabled = DISABLED;

        // Act
        _wrapper.Clear(true);

        // Assert
        _mockConsole.DidNotReceive().Clear(Arg.Any<bool>());
    }

    [Fact]
    public void Write_WhenEnabled_DelegatesToInnerConsole()
    {
        // Arrange
        _wrapper.Enabled = ENABLED;
        var renderable = Substitute.For<IRenderable>();

        // Act
        _wrapper.Write(renderable);

        // Assert
        _mockConsole.Received(1).Write(renderable);
    }

    [Fact]
    public void Write_WhenLoggerConfigured_WritesToLogger()
    {
        // Arrange
        AnsiConsoleWrapper wrapper = new(new TestConsole());
        wrapper.SetLogger(_logger);
        wrapper.Enabled = ENABLED;
        const string MSG = "test";
        var renderable = new Paragraph(MSG);

        // Act
        wrapper.Write(renderable);

        // Assert
        _logger.Received(1).Information("{@Msg}", MSG);
    }

    [Fact]
    public void Write_WhenDisabled_DoesNotDelegate()
    {
        // Arrange
        _wrapper.Enabled = DISABLED;
        var renderable = Substitute.For<IRenderable>();

        // Act
        _wrapper.Write(renderable);

        // Assert
        _mockConsole.DidNotReceive().Write(Arg.Any<IRenderable>());
    }

    [Fact]
    public void Properties_DelegatesToInnerConsole()
    {
        // Arrange
        var console = new TestConsole();
        var profile = console.Profile;
        var cursor = console.Cursor;
        var input = console.Input;
        var exclusivityMode = console.ExclusivityMode;
        var pipeline = console.Pipeline;

        _mockConsole.Profile.Returns(profile);
        _mockConsole.Cursor.Returns(cursor);
        _mockConsole.Input.Returns(input);
        _mockConsole.ExclusivityMode.Returns(exclusivityMode);
        _mockConsole.Pipeline.Returns(pipeline);

        // Act & Assert
        _wrapper.Profile.ShouldBe(profile);
        _wrapper.Cursor.ShouldBe(cursor);
        _wrapper.Input.ShouldBe(input);
        _wrapper.ExclusivityMode.ShouldBe(exclusivityMode);
        _wrapper.Pipeline.ShouldBe(pipeline);
    }
}
