using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TableCloth3.Shared.Models;
using TableCloth3.Shared.Services;

namespace TableCloth3;

internal static class McpServerTools
{
    public static readonly McpServerTool[] AvailableTools = [
        McpServerTool.Create(GetCurrentMode),
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

    public static THostApplicationBuilder AddMcpServer<THostApplicationBuilder>(this THostApplicationBuilder builder)
        where THostApplicationBuilder : IHostApplicationBuilder
    {
        var mcpServerBuilder = builder.Services.AddMcpServer();
        mcpServerBuilder.WithTools(AvailableTools);

        if (builder is WebApplicationBuilder webAppBuilder)
        {
            mcpServerBuilder.WithHttpTransport();
            webAppBuilder.WebHost.UseUrls("http://127.0.0.1:0;http://[::1]:0");
            // To do: Create secondary web application with YARP for 29400/tcp
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
