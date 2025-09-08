using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using TableCloth3.Shared.Services;

namespace TableCloth3;

internal partial class App : Application
{
    [ActivatorUtilitiesConstructor]
    public App(
        AvaloniaWindowManager windowManager)
        : this()
    {
        _windowManager = windowManager;
    }

    public App()
        : base()
    {
    }

    private readonly AvaloniaWindowManager _windowManager = default!;

    public override void Initialize()
        => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        RequestedThemeVariant = ThemeVariant.Default;

        if (!Design.IsDesignMode)
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = _windowManager.CreateMainAvaloniaWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
