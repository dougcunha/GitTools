using Spectre.Console;
using Spectre.Console.Rendering;

namespace GitTools.Services;

/// <summary>
/// Provides a wrapper around an <see cref="IAnsiConsole"/> instance, allowing for conditional console operations based
/// on the <see cref="Enabled"/> property.
/// </summary>
/// <remarks>This class delegates console operations to the underlying <see cref="IAnsiConsole"/> instance.
/// Operations are only performed if the <see cref="Enabled"/> property is set to <see langword="true"/>.</remarks>
/// <param name="ansiConsole">
/// An instance of <see cref="IAnsiConsole"/> that this wrapper will delegate calls to.
/// </param>
public sealed class AnsiConsoleWrapper(IAnsiConsole ansiConsole) : IAnsiConsole
{
    public bool Enabled { get; set; }

    /// <inheritdoc />
    public void Clear(bool home)
    {
        if (!Enabled)
            return;

        ansiConsole.Clear(home);
    }

    /// <inheritdoc />
    public void Write(IRenderable renderable)
    {
        if (!Enabled)
            return;

        ansiConsole.Write(renderable);
    }

    /// <inheritdoc />
    public Profile Profile
        => ansiConsole.Profile;

    /// <inheritdoc />
    public IAnsiConsoleCursor Cursor
        => ansiConsole.Cursor;

    /// <inheritdoc />
    public IAnsiConsoleInput Input
        => ansiConsole.Input;

    /// <inheritdoc />
    public IExclusivityMode ExclusivityMode
        => ansiConsole.ExclusivityMode;

    /// <inheritdoc />
    public RenderPipeline Pipeline
        => ansiConsole.Pipeline;
}
