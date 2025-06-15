using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;

namespace GitTools.Services;

/// <inheritdoc/>
[ExcludeFromCodeCoverage]
public sealed class ZipBackupService : IBackupService
{
    /// <inheritdoc/>
    public void CreateBackup(string sourceDirectory, string destinationZipFile)
    {
        if (File.Exists(destinationZipFile))
            File.Delete(destinationZipFile);

        ZipFile.CreateFromDirectory(sourceDirectory, destinationZipFile, CompressionLevel.Fastest, includeBaseDirectory: true);
    }
}
