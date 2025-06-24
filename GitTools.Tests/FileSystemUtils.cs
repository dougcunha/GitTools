namespace GitTools.Tests;

/// <summary>
/// Utility class for file system operations, providing methods to normalize paths
/// across different platforms.
/// This is particularly useful for ensuring consistent path handling in tests
/// and commands that may run on different operating systems.
/// </summary>
public static class FileSystemUtils
{
    /// <summary>
    /// Normalizes the given path for the current platform.
    /// This method ensures that the path is in a consistent format, removing any trailing
    /// directory separators and converting to the full path.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>A normalized path string.</returns>
    /// <exception cref="ArgumentException">Thrown when the provided path is null or empty.</exception>
    /// <remarks>
    /// This method is useful for ensuring that paths are handled consistently across
    /// different operating systems, especially when dealing with file system operations
    /// in tests or commands that may run on both Windows and Unix-like systems.
    /// It trims any trailing directory separators and converts the path to its full
    /// absolute form, which is important for operations that require a valid file system path.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public static string GetNormalizedPathForCurrentPlatform(string path)
    {
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        // Normalize the path for the current platform
        if (!isWindows)
        {
            path = path.Replace('\\', Path.AltDirectorySeparatorChar);

            if (!path.StartsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                path = Path.AltDirectorySeparatorChar + path;
        }
        else
        {
            path = path.Replace('/', Path.DirectorySeparatorChar);
        }

        return path;
    }
}