using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using System.Runtime.InteropServices;
using TableCloth3.Shared.Services;
using TableCloth3.Shared.ViewModels;
using TableCloth3.Shared.Windows;

namespace TableCloth3.Shared;

internal static class SharedHostExtensions
{
    internal static readonly string CatalogHttpClient = nameof(CatalogHttpClient);

    public static HttpClient CreateCatalogHttpClient(this IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient(CatalogHttpClient);

        var appName = Assembly.GetEntryAssembly()?.GetName().Name ?? "TableClothVariant";
        var appVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
        var os = RuntimeInformation.OSDescription.Trim();
        var arch = RuntimeInformation.OSArchitecture.ToString();

        client.DefaultRequestHeaders.Add("User-Agent", $"{appName}/{appVersion} ({os}; {arch})");
        return client;
    }

    internal static readonly string ChromeHttpClient = nameof(ChromeHttpClient);
    internal static readonly string[] WellKnownChromeUserAgentStrings =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:139.0) Gecko/20100101 Firefox/139.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/138.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36",
    ];

    public static HttpClient CreateChromeHttpClient(this IHttpClientFactory httpClientFactory)
    {
        var client = httpClientFactory.CreateClient(ChromeHttpClient);
        var selectedUserAgent = WellKnownChromeUserAgentStrings[Random.Shared.Next(WellKnownChromeUserAgentStrings.Length)];
        client.DefaultRequestHeaders.Add("User-Agent", selectedUserAgent);
        return client;
    }

    public static IHostApplicationBuilder UseTableCloth3SharedComponents(
        this IHostApplicationBuilder builder,
        string[] args,
        out ScenarioRouter scenarioRouter)
    {
        builder.Configuration.AddCommandLine(args);
        builder.Configuration.AddEnvironmentVariables();

        scenarioRouter = new ScenarioRouter(builder.Configuration);
        builder.Services.AddSingleton(scenarioRouter);

        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<AppSettingsManager>();
        builder.Services.AddSingleton<TableClothCatalogService>();

        builder.Services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        builder.Services.AddSingleton<AvaloniaViewModelManager>();
        builder.Services.AddSingleton<AvaloniaWindowManager>();

        builder.Services.AddSingleton<ProcessManagerFactory>();
        builder.Services.AddSingleton<McpServerStatusService>();

        builder.Services.AddHttpClient(CatalogHttpClient, client =>
        {
            client.BaseAddress = new Uri("https://yourtablecloth.app", UriKind.Absolute);
        });

        builder.Services.AddSingleton<ArchiveExpander>();

        builder.Services.AddTransient<AboutWindowViewModel>();
        builder.Services.AddTransient<AboutWindow>();

        return builder;
    }
}
