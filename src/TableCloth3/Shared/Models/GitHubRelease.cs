using System.Text.Json.Serialization;

namespace TableCloth3.Shared.Models;

public sealed record GitHubRelease
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("tag_name")]
    public string TagName { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; init; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    [JsonPropertyName("draft")]
    public bool Draft { get; init; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; init; } = string.Empty;

    [JsonPropertyName("assets")]
    public GitHubReleaseAsset[] Assets { get; init; } = [];
}

public sealed record GitHubReleaseAsset
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("download_count")]
    public int DownloadCount { get; init; }
}