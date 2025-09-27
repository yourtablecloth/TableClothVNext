using System.Text.Json.Serialization;
using TableCloth3.Shared.Models;

namespace TableCloth3.Shared.Json;

[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubReleaseAsset))]
[JsonSerializable(typeof(GitHubReleaseAsset[]))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    GenerationMode = JsonSourceGenerationMode.Default)]
public partial class GitHubJsonSerializerContext : JsonSerializerContext
{
}