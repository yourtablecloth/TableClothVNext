using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting.Internal;
using TableCloth3.Help.Windows;
using TableCloth3.Launcher.Windows;
using TableCloth3.Shared.Models;
using TableCloth3.Spork.Windows;

namespace TableCloth3.Shared.Services;

public sealed class AvaloniaWindowManager
{
    public AvaloniaWindowManager(
        IServiceProvider serviceProvider,
        ScenarioRouter scenarioRouter)
    {
        _serviceProvider = serviceProvider;
        _scenarioRouter = scenarioRouter;
    }

    private readonly IServiceProvider _serviceProvider;
    private readonly ScenarioRouter _scenarioRouter;

    public TWindow GetAvaloniaWindow<TWindow>()
        where TWindow : Window
        => _serviceProvider.GetRequiredService<TWindow>();

    public Window CreateMainAvaloniaWindow()
        => _scenarioRouter.GetScenario() switch
        {
            Scenario.Help => GetAvaloniaWindow<HelpMainWindow>(),
            Scenario.Spork => GetAvaloniaWindow<SporkMainWindow>(),
            _ => GetAvaloniaWindow<LauncherMainWindow>(),
        };

    public Window GetMainWindow()
    {
        if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop?.MainWindow;
            if (mainWindow != null)
                return mainWindow;
        }

        throw new Exception("Cannot obtain the main window reference.");
    }
}
