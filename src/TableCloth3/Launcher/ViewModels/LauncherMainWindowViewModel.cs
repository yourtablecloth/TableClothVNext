using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using TableCloth3.Launcher.Services;
using TableCloth3.Shared.Services;
using TableCloth3.Shared.ViewModels;

namespace TableCloth3.Launcher.ViewModels;

public sealed partial class LauncherMainWindowViewModel : BaseViewModel
{
    [ActivatorUtilitiesConstructor]
    public LauncherMainWindowViewModel(
  IMessenger messenger,
        AvaloniaViewModelManager viewModelManager,
    LauncherSettingsManager launcherSettingsManager,
        WindowsSandboxComposer windowsSandboxComposer,
        WindowsSandboxLauncher windowsSandboxLauncher,
        TableClothCatalogService tableClothCatalogService,
        ProcessManagerFactory processManagerFactory,
        AvaloniaWindowManager windowManager,
   GitHubUpdateService gitHubUpdateService)
    {
        _messenger = messenger;
        _viewModelManager = viewModelManager;
        _launcherSettingsManager = launcherSettingsManager;
        _windowsSandboxComposer = windowsSandboxComposer;
 _windowsSandboxLauncher = windowsSandboxLauncher;
  _tableClothCatalogService = tableClothCatalogService;
     _processManagerFactory = processManagerFactory;
        _windowManager = windowManager;
        _gitHubUpdateService = gitHubUpdateService;
    }

 public LauncherMainWindowViewModel() { }

    private readonly IMessenger _messenger = default!;
    private readonly AvaloniaViewModelManager _viewModelManager = default!;
    private readonly LauncherSettingsManager _launcherSettingsManager = default!;
    private readonly WindowsSandboxComposer _windowsSandboxComposer = default!;
  private readonly WindowsSandboxLauncher _windowsSandboxLauncher = default!;
    private readonly TableClothCatalogService _tableClothCatalogService = default!;
    private readonly ProcessManagerFactory _processManagerFactory = default!;
    private readonly AvaloniaWindowManager _windowManager = default!;
    private readonly GitHubUpdateService _gitHubUpdateService = default!;

    public sealed record class ShowDisclaimerWindowMessage();

    public interface IShowDisclaimerWindowMessageRecipient : IRecipient<ShowDisclaimerWindowMessage>;

    public sealed record class AboutButtonMessage;

    public interface IAboutButtonMessageRecipient : IRecipient<AboutButtonMessage>;

    public sealed record class ManageFolderButtonMessage(ObservableCollection<string> Folders);

  public interface IManageFolderButtonMessageRecipient : IRecipient<ManageFolderButtonMessage>;

    public sealed record class NotifyErrorMessage(Exception FoundException);

    public interface INotifyErrorMessageRecipient : IRecipient<NotifyErrorMessage>;

    public sealed record class NotifyWarningsMessage(IEnumerable<string> FoundWarnings);

    public interface INotifyWarningsMessageRecipient : IRecipient<NotifyWarningsMessage>;

    public sealed record class ShowUpdateAvailableMessage(string Version, string ReleaseUrl);

    public interface IShowUpdateAvailableMessageRecipient : IRecipient<ShowUpdateAvailableMessage>;

  public sealed record class SponsorButtonMessage;

    public interface ISponsorButtonMessageRecipient : IRecipient<SponsorButtonMessage>;

    [ObservableProperty]
    private bool _useMicrophone = false;

 [ObservableProperty]
    private bool _useWebCamera = false;

    [ObservableProperty]
    private bool _sharePrinters = false;

    [ObservableProperty]
    private bool _mountNpkiFolders = true;

    [ObservableProperty]
 private bool _mountSpecificFolders = false;

    [ObservableProperty]
    private DateTime? _disclaimerAccepted = default;

    [ObservableProperty]
 private ObservableCollection<string> _folders = new ObservableCollection<string>();

    [ObservableProperty]
    private bool _loading = false;

    [RelayCommand]
    private void AboutButton()
        => _messenger.Send<AboutButtonMessage>();

    [RelayCommand]
    private void SponsorButton()
    => _messenger.Send<SponsorButtonMessage>();

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LaunchButton(CancellationToken cancellationToken = default)
    {
        try
        {
            var warnings = await _windowsSandboxLauncher.LaunchWindowsSandboxAsync(this, null, cancellationToken);

            if (warnings.Any())
         _messenger.Send<NotifyWarningsMessage>(new NotifyWarningsMessage(warnings));
        }
  catch (Exception ex)
        {
      _messenger.Send<NotifyErrorMessage>(new NotifyErrorMessage(ex));
        }
 }

    [RelayCommand]
    private void ManageFolderButton()
        => _messenger.Send<ManageFolderButtonMessage>(new ManageFolderButtonMessage(Folders));

 [RelayCommand]
    private async Task Loaded(CancellationToken cancellationToken = default)
    {
        if (IsDesignMode)
     return;

        Loading = true;

        await Task.WhenAll([
     _tableClothCatalogService.DownloadCatalogAsync(cancellationToken),
 _tableClothCatalogService.DownloadImagesAsync(cancellationToken),
            CheckForUpdatesInBackground(cancellationToken),
  ]);

        Loading = false;
    }

    /// <summary>
    /// Check for updates in the background without blocking the UI
    /// </summary>
    private async Task CheckForUpdatesInBackground(CancellationToken cancellationToken = default)
    {
        if (_gitHubUpdateService == null)
            return;

     try
        {
   // Add a small delay to not overwhelm the startup
       await Task.Delay(2000, cancellationToken);

            var release = await _gitHubUpdateService.CheckForUpdatesAsync(cancellationToken);

     if (release != null && _gitHubUpdateService.IsUpdateAvailable(release))
            {
     // Send message to show update notification
                _messenger.Send(new ShowUpdateAvailableMessage(release.TagName, release.HtmlUrl));
   }
      }
  catch
        {
// Silently ignore update check failures during startup
        }
    }
}
