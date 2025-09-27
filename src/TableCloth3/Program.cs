using AsyncAwaitBestPractices;
using Avalonia;
using Lemon.Hosting.AvaloniauiDesktop;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;
using TableCloth3.Help;
using TableCloth3.Launcher;
using TableCloth3.Shared;
using TableCloth3.Shared.Models;
using TableCloth3.Shared.Services;
using TableCloth3.Spork;

namespace TableCloth3;

public static class Program
{
    [STAThread]
    [SupportedOSPlatform("windows")]
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.UseTableCloth3SharedComponents(args, out var scenarioRouter);
        builder.AddMcpServer();

        // Register singleton service
        builder.Services.AddSingleton<ISingleInstanceService, SingleInstanceService>();

        switch (scenarioRouter.GetScenario())
        {
            default:
            case Scenario.Launcher:
                builder.UseTableCloth3LauncherComponents();
                break;

            case Scenario.Spork:
                builder.UseTableCloth3SporkComponents();
                break;

            case Scenario.Help:
                builder.UseTableCloth3HelpComponents();
                break;
        }

        builder.Services.AddAvaloniauiDesktopApplication<App>(BuildAvaloniaApp);

        using var app = builder.Build();

        // Check for existing instance before proceeding
        var singleInstanceService = app.Services.GetRequiredService<ISingleInstanceService>();
        if (singleInstanceService.IsAnotherInstanceRunning())
        {
            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("TableCloth3.Program");
            logger.LogInformation("Another instance of TableCloth3 is already running. Attempting to bring it to foreground.");

            singleInstanceService.BringExistingInstanceToForeground();

            // Exit gracefully
            return;
        }

        app.TryMapMcpServer();

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var logger = app.Services.GetRequiredService<ILogger<SecondaryWebHostManager>>();

            try
            {
                var server = app.Services.GetRequiredService<IServer>();
                var addressResolver = server.Features.GetRequiredFeature<IServerAddressesFeature>();

                var firstAddress = addressResolver.Addresses.FirstOrDefault();
                if (!string.IsNullOrEmpty(firstAddress))
                {
                    logger.LogInformation("MCP server started at {Address}.", firstAddress);

                    var webHostManager = app.Services.GetRequiredService<SecondaryWebHostManager>();

                    // Start YARP proxy server
                    Task.Run(async () =>
                    {
                        try
                        {
                            logger.LogInformation("Starting YARP proxy server... (Target: {TargetAddress})", firstAddress);
                            var proxyApp = await webHostManager.StartSecondaryWebHost(firstAddress);

                            if (proxyApp != null)
                            {
                                logger.LogInformation("YARP proxy server successfully started at http://127.0.0.1:29400.");
                            }
                            else
                            {
                                logger.LogError("Failed to start YARP proxy server.");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "An error occurred while starting YARP proxy server.");
                        }
                    }).SafeFireAndForget();
                }
                else
                {
                    logger.LogWarning("Could not find MCP server address.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while starting the server.");
            }
        });

        // Register cleanup on application shutdown
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("TableCloth3.Program");
            logger.LogInformation("Application is shutting down, releasing singleton lock...");
            singleInstanceService.ReleaseLock();
        });

        app.RunAvaloniauiApplication(args).GetAwaiter().GetResult();
    }

    // This method is used by both AppHost Avalonia runtime and the Avalonia Designer.
    private static AppBuilder BuildAvaloniaApp(AppBuilder? app)
    {
        if (app == null)
            app = AppBuilder.Configure<App>();

        return app
            .UsePlatformDetect()
            .LogToTrace();
    }

    // This method is required for use with the Avalonia designer.
    // The Avalonia designer will look for this method regardless of whether or not it is private.
    private static AppBuilder BuildAvaloniaApp()
        => BuildAvaloniaApp(null);
}
