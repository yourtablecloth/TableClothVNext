using AsyncAwaitBestPractices;
using Avalonia;
using Lemon.Hosting.AvaloniauiDesktop;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                    logger.LogInformation("MCP 서버가 {Address}에서 시작되었습니다.", firstAddress);
                    
                    var webHostManager = app.Services.GetRequiredService<SecondaryWebHostManager>();
                    
                    // YARP 프록시 서버 시작
                    Task.Run(async () =>
                    {
                        try
                        {
                            logger.LogInformation("YARP 프록시 서버를 시작하는 중... (대상: {TargetAddress})", firstAddress);
                            var proxyApp = await webHostManager.StartSecondaryWebHost(firstAddress);
                            
                            if (proxyApp != null)
                            {
                                logger.LogInformation("YARP 프록시 서버가 http://127.0.0.1:29400에서 성공적으로 시작되었습니다.");
                            }
                            else
                            {
                                logger.LogError("YARP 프록시 서버 시작에 실패했습니다.");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "YARP 프록시 서버 시작 중 오류가 발생했습니다.");
                        }
                    }).SafeFireAndForget();
                }
                else
                {
                    logger.LogWarning("MCP 서버 주소를 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "서버 시작 중 오류가 발생했습니다.");
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
