using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System.ComponentModel;
using TableCloth3.Launcher.Services;
using TableCloth3.Launcher.ViewModels;
using TableCloth3.Shared.Models;
using TableCloth3.Shared.Services;

namespace TableCloth3;

internal static class McpServerTools
{
    public static readonly McpServerTool[] AvailableTools = [
        McpServerTool.Create(GetCurrentMode),
        McpServerTool.Create(LaunchSite),
    ];

    [Description("Get the current mode of the application, such as Launcher, Spork, or Help.")]
    public static string GetCurrentMode(
        IServiceProvider serviceProvider)
    {
        var scenarioRouter = serviceProvider.GetRequiredService<ScenarioRouter>();
        return scenarioRouter.GetScenario() switch
        {
            Scenario.Launcher => "Launcher",
            Scenario.Spork => "Spork",
            Scenario.Help => "Help",
            _ => "Unknown"
        };
    }

    public static async Task<string> LaunchSite(
        IServiceProvider serviceProvider,
        string url,
        CancellationToken cancellationToken = default)
    {
        var scenarioRouter = serviceProvider.GetRequiredService<ScenarioRouter>();

        if (scenarioRouter.GetScenario() != Scenario.Launcher)
            return "This MCP operation is not supported in this mode.";

        var vm = serviceProvider.GetRequiredService<LauncherMainWindowViewModel>();
        var launcher = serviceProvider.GetRequiredService<WindowsSandboxLauncher>();
        var warnings = await launcher.LaunchWindowsSandboxAsync(vm, url, cancellationToken).ConfigureAwait(false);

        if (warnings.Any())
            return "Launched with warnings: " + string.Join("; ", warnings);
        else
            return "Launched successfully.";
    }

    public static THostApplicationBuilder AddMcpServer<THostApplicationBuilder>(this THostApplicationBuilder builder)
        where THostApplicationBuilder : IHostApplicationBuilder
    {
        var mcpServerBuilder = builder.Services.AddMcpServer();
        mcpServerBuilder.WithTools(AvailableTools);

        if (builder is WebApplicationBuilder webAppBuilder)
        {
            mcpServerBuilder.WithHttpTransport();
            webAppBuilder.WebHost.UseUrls("http://127.0.0.1:0;http://[::1]:0");
            builder.Services.AddSingleton<SecondaryWebHostManager>();
        }
        else
            mcpServerBuilder.WithStdioServerTransport();

        return builder;
    }

    public static THost TryMapMcpServer<THost>(this THost host)
        where THost : IHost
    {
        if (host is WebApplication webApp)
            webApp.MapMcp();

        return host;
    }
}
