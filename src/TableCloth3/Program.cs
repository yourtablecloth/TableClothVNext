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
        app.TryMapMcpServer();

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addressResolver = server.Features.GetRequiredFeature<IServerAddressesFeature>();

            var webHostManager = app.Services.GetRequiredService<SecondaryWebHostManager>();
            webHostManager.StartSecondaryWebHost(addressResolver.Addresses.First()).SafeFireAndForget();
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
