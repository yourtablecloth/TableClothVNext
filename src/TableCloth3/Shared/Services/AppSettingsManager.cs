using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TableCloth3.Shared.Services;

public sealed class AppSettingsManager
{
    public AppSettingsManager(
        ILogger<AppSettingsManager> logger,
        LocationService locationService)
        : base()
    {
        _logger = logger;
        _locationService = locationService;
    }

    private readonly ILogger<AppSettingsManager> _logger = default!;
    private readonly LocationService _locationService = default!;

    public async Task<TModel?> LoadAsync<TJsonSerializerContext, TModel>(
        TJsonSerializerContext context,
        string fileName,
        CancellationToken cancellationToken = default)
        where TJsonSerializerContext : JsonSerializerContext
        where TModel : class
    {
        if (string.IsNullOrWhiteSpace(fileName))
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var typeInfo = context.GetTypeInfo(typeof(TModel));
        if (typeInfo == null)
            throw new NotSupportedException($"Selected type '{typeof(TModel)}' is not supported.");
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            fileName = string.Concat(fileName, ".json");

        var targetEncoding = new UTF8Encoding(false);
        var directoryPath = _locationService.EnsureAppDataDirectoryCreated().FullName;
        var filePath = Path.Combine(directoryPath, fileName);

        try
        {
            var content = await File.ReadAllTextAsync(filePath, targetEncoding, cancellationToken).ConfigureAwait(false);
            var items = JsonSerializer.Deserialize(content, typeInfo) as TModel;
            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cannot load settings from file '{filepath}' due to error.", filePath);
            return default;
        }
    }

    public async Task SaveAsync<TJsonSerializerContext, TModel>(
        TJsonSerializerContext context,
        TModel model,
        string fileName,
        CancellationToken cancellationToken = default)
        where TJsonSerializerContext : JsonSerializerContext
        where TModel : class
    {
        if (string.IsNullOrWhiteSpace(fileName))
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            fileName = string.Concat(fileName, ".json");

        var directoryPath = _locationService.EnsureAppDataDirectoryCreated().FullName;
        var filePath = Path.Combine(directoryPath, fileName);
        using var fileStream = File.Open(filePath, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(fileStream, model, typeof(TModel), context, cancellationToken).ConfigureAwait(false);
    }
}
