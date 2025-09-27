using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using TableCloth3.Shared.Models;

namespace TableCloth3.Shared.Services;

public sealed class GitHubUpdateService
{
    private const string GitHubApiBaseUrl = "https://api.github.com";
    private const string RepositoryOwner = "yourtablecloth";
    private const string RepositoryName = "TableCloth";

    public GitHubUpdateService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Gets the current application version
    /// </summary>
    public Version GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version ?? new Version(0, 0, 0, 0);
    }

    /// <summary>
    /// Checks if a newer version is available on GitHub
    /// </summary>
    public async Task<GitHubRelease?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateCatalogHttpClient();

            // GitHub API requires User-Agent header to be set
            var response = await httpClient.GetAsync(
                $"{GitHubApiBaseUrl}/repos/{RepositoryOwner}/{RepositoryName}/releases/latest",
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var release = JsonSerializer.Deserialize<GitHubRelease>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return release;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Compares current version with a release version to determine if update is available
    /// </summary>
    public bool IsUpdateAvailable(GitHubRelease release)
    {
        if (release == null)
            return false;

        var currentVersion = GetCurrentVersion();

        // Parse the tag name (e.g., "v1.14.0" -> "1.14.0")
        var tagVersion = ParseVersionFromTag(release.TagName);
        if (tagVersion == null)
            return false;

        return tagVersion > currentVersion;
    }

    /// <summary>
    /// Opens the GitHub release page in the default browser
    /// </summary>
    public void OpenReleasePage(GitHubRelease release)
    {
        if (release == null || string.IsNullOrWhiteSpace(release.HtmlUrl))
            return;

        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = release.HtmlUrl,
                UseShellExecute = true
            };
            Process.Start(processStartInfo);
        }
        catch
        {
            // Ignore if browser can't be opened
        }
    }

    /// <summary>
    /// Opens the GitHub releases page in the default browser
    /// </summary>
    public void OpenReleasesPage()
    {
        try
        {
            var releasesUrl = $"https://github.com/{RepositoryOwner}/{RepositoryName}/releases";
            var processStartInfo = new ProcessStartInfo
            {
                FileName = releasesUrl,
                UseShellExecute = true
            };
            Process.Start(processStartInfo);
        }
        catch
        {
            // Ignore if browser can't be opened
        }
    }

    /// <summary>
    /// Parses version information from GitHub release tag
    /// </summary>
    private static Version? ParseVersionFromTag(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return null;

        // Remove 'v' prefix if present
        var versionString = tagName.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            ? tagName[1..]
            : tagName;

        return Version.TryParse(versionString, out var version) ? version : null;
    }
}