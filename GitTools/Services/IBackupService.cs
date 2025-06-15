namespace GitTools.Services;

/// <summary>
/// Provides backup capabilities for directories.
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a zip archive from the specified directory.
    /// </summary>
    /// <param name="sourceDirectory">Directory to archive.</param>
    /// <param name="destinationZipFile">Path of the resulting zip file.</param>
    void CreateBackup(string sourceDirectory, string destinationZipFile);
}
