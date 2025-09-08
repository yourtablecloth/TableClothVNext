using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TableCloth3.Launcher.Services;
using TableCloth3.Launcher.ViewModels;
using TableCloth3.Launcher.Windows;

namespace TableCloth3.Launcher;

internal static class LauncherHostExtensions
{
    public static IHostApplicationBuilder UseTableCloth3LauncherComponents(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<WindowsSandboxLauncher>();
        builder.Services.AddSingleton<WindowsSandboxComposer>();
        builder.Services.AddSingleton<LauncherSettingsManager>();
        builder.Services.AddSingleton<EnvironmentValidationService>();

        builder.Services.AddSingleton<DisclaimerWindowViewModel>();
        builder.Services.AddSingleton<DisclaimerWindow>();

        builder.Services.AddTransient<FolderManageWindowViewModel>();
        builder.Services.AddTransient<FolderManageWindow>();

        builder.Services.AddTransient<LauncherMainWindowViewModel>();
        builder.Services.AddTransient<LauncherMainWindow>();

        return builder;
    }
}
