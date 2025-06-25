using System.Text.Json.Serialization;
using GitTools.Models;

namespace GitTools.Json;

/// <summary>
/// JSON serializer context for AOT compatibility.
/// </summary>
[JsonSerializable(typeof(GitToolsOptions))]
[JsonSerializable(typeof(GitRepository))]
[JsonSerializable(typeof(GitRepositoryBackup))]
[JsonSerializable(typeof(List<GitRepository>))]
[JsonSerializable(typeof(List<GitRepositoryBackup>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
)]
public sealed partial class GitToolsJsonContext : JsonSerializerContext
{
}
