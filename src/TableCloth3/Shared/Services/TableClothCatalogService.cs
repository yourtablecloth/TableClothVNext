﻿using System.Text.Json;
using System.Xml.Linq;
using TableCloth3.Shared.Languages;

namespace TableCloth3.Shared.Services;

public sealed class TableClothCatalogService
{
    public TableClothCatalogService(
        IHttpClientFactory httpClientFactory,
        ArchiveExpander archiveExpander,
        LocationService locationService)
        : base()
    {
        _httpClientFactory = httpClientFactory;
        _archiveExpander = archiveExpander;
        _locationService = locationService;

        _imageDirectoryPath = _locationService.EnsureImagesDirectoryCreated().FullName;
    }

    private readonly IHttpClientFactory _httpClientFactory = default!;
    private readonly ArchiveExpander _archiveExpander = default!;
    private readonly LocationService _locationService = default!;

    private readonly string _imageDirectoryPath = default!;

    public async Task<XDocument> LoadCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateCatalogHttpClient();
        using var contentStream = await httpClient.GetStreamAsync($"/TableClothCatalog/Catalog.xml?ts={Uri.EscapeDataString(DateTime.UtcNow.Ticks.ToString())}", cancellationToken).ConfigureAwait(false);
        return await XDocument.LoadAsync(contentStream, default, cancellationToken).ConfigureAwait(false);
    }

    public string GetCatalogDisplayName(string? rawName)
        => rawName?.ToUpperInvariant() switch
        {
            "BANKING" => SharedStrings.BankingCategoryDisplayName,
            "FINANCING" => SharedStrings.FinancingCategoryDisplayName,
            "SECURITY" => SharedStrings.SecurityCategoryDisplayName,
            "CREDITCARD" => SharedStrings.CreditCardCategoryDisplayName,
            "INSURANCE" => SharedStrings.InsuranceCategoryDisplayName,
            "GOVERNMENT" => SharedStrings.GovernmentCategoryDisplayName,
            "EDUCATION" => SharedStrings.EducationCategoryDisplayName,
            _ => SharedStrings.OtherCategoryDisplayName,
        };

    public async Task LoadImagesAsync(
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateCatalogHttpClient();
        var ts = Uri.EscapeDataString(DateTime.UtcNow.Ticks.ToString());
        var imageUpdateRequired = false;

        var remoteBuildInfoJson = await httpClient.GetStringAsync(
            $"/TableClothCatalog/build-info.json?ts={ts}",
            cancellationToken).ConfigureAwait(false);
        var remoteBuildInfoDoc = JsonDocument.Parse(remoteBuildInfoJson);
        var remoteCommitId = remoteBuildInfoDoc.RootElement.GetProperty("commit_id").GetString();

        var localBuildInfoPath = Path.Combine(
            _locationService.EnsureAppDataDirectoryCreated().FullName,
            "build-info.json");

        if (File.Exists(localBuildInfoPath))
        {
            try
            {
                var localBuildInfoJson = await File
                    .ReadAllTextAsync(localBuildInfoPath, cancellationToken)
                    .ConfigureAwait(false);
                var localBuildInfoDoc = JsonDocument.Parse(localBuildInfoJson);
                var localCommitId = localBuildInfoDoc.RootElement.GetProperty("commit_id").GetString();

                if (!string.Equals(remoteCommitId, localCommitId, StringComparison.Ordinal))
                    imageUpdateRequired = true;
            }
            catch
            {
                imageUpdateRequired = true;
            }
        }
        else
            imageUpdateRequired = true;

        if (!imageUpdateRequired)
            return;

        await File.WriteAllTextAsync(
            localBuildInfoPath, remoteBuildInfoJson, cancellationToken)
            .ConfigureAwait(false);

        using var imagePackContentStream = await httpClient.GetStreamAsync(
            $"/TableClothCatalog/Images.zip?ts={ts}",
            cancellationToken).ConfigureAwait(false);

        var targetDirectoryToExtract = _locationService.EnsureImagesDirectoryCreated().FullName;
        var downloadPath = Path.Combine(targetDirectoryToExtract, "Images.zip");
        using var localStream = File.Open(downloadPath, FileMode.Create, FileAccess.ReadWrite);
        await imagePackContentStream.CopyToAsync(localStream, cancellationToken).ConfigureAwait(false);
        localStream.Seek(0L, SeekOrigin.Begin);

        targetDirectoryToExtract = Directory.CreateDirectory(targetDirectoryToExtract).FullName;
        await _archiveExpander.ExpandArchiveAsync(localStream, targetDirectoryToExtract, cancellationToken).ConfigureAwait(false);
    }

    public string GetLocalImagePath(string serviceId)
        => Path.GetFullPath(Path.Combine(_imageDirectoryPath, serviceId + ".png"));

    public string GetLocalIconPath(string serviceId)
        => Path.GetFullPath(Path.Combine(_imageDirectoryPath, serviceId + ".ico"));
}
