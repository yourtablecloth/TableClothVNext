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
        var targetDirectory = _locationService.EnsureAppDataDirectoryCreated().FullName;
        var downloadPath = Path.Combine(targetDirectory, "Catalog.xml");

        if (!File.Exists(downloadPath))
            throw new FileNotFoundException($"Local catalog file '{downloadPath}' does not exists.");

        using var contentStream = File.OpenRead(downloadPath);
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

    public async Task DownloadCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateCatalogHttpClient();
        var ts = Uri.EscapeDataString(DateTime.UtcNow.Ticks.ToString());

        using var imagePackContentStream = await httpClient.GetStreamAsync(
            $"/TableClothCatalog/Catalog.xml?ts={ts}",
            cancellationToken).ConfigureAwait(false);

        var targetDirectory = _locationService.EnsureAppDataDirectoryCreated().FullName;
        var downloadPath = Path.Combine(targetDirectory, "Catalog.xml");
        using var localStream = File.Open(downloadPath, FileMode.Create, FileAccess.ReadWrite);
        await imagePackContentStream.CopyToAsync(localStream, cancellationToken).ConfigureAwait(false);
        localStream.Seek(0L, SeekOrigin.Begin);
    }

    public async Task DownloadImagesAsync(
        CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateCatalogHttpClient();
        var ts = Uri.EscapeDataString(DateTime.UtcNow.Ticks.ToString());

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
