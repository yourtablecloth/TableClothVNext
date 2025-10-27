using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using TableCloth3.Launcher.Languages;
using TableCloth3.Launcher.Models;
using TableCloth3.Launcher.Services;
using TableCloth3.Launcher.ViewModels;
using TableCloth3.Shared.Services;
using TableCloth3.Shared.Windows;

using static TableCloth3.Launcher.ViewModels.LauncherMainWindowViewModel;

namespace TableCloth3.Launcher.Windows;

public partial class LauncherMainWindow :
    Window,
    IShowDisclaimerWindowMessageRecipient,
    IAboutButtonMessageRecipient,
    ISponsorButtonMessageRecipient,
    IManageFolderButtonMessageRecipient,
    INotifyErrorMessageRecipient,
    INotifyWarningsMessageRecipient,
    IShowUpdateAvailableMessageRecipient
{
    [ActivatorUtilitiesConstructor]
    public LauncherMainWindow(
        LauncherMainWindowViewModel viewModel,
        IMessenger messenger,
        AvaloniaWindowManager windowManager,
        LauncherSettingsManager launcherSettingsManager,
        ProcessManagerFactory processManagerFactory)
        : this()
    {
        _viewModel = viewModel;
        _messenger = messenger;
        _windowManager = windowManager;
        _launcherSettingsManager = launcherSettingsManager;
        _processManagerFactory = processManagerFactory;

        DataContext = _viewModel;

        _messenger.Register<ShowDisclaimerWindowMessage>(this);
        _messenger.Register<AboutButtonMessage>(this);
        _messenger.Register<SponsorButtonMessage>(this);
        _messenger.Register<ManageFolderButtonMessage>(this);
        _messenger.Register<NotifyErrorMessage>(this);
        _messenger.Register<NotifyWarningsMessage>(this);
        _messenger.Register<ShowUpdateAvailableMessage>(this);

        //ShowAsDialog = true;
    }

    public LauncherMainWindow()
        : base()
    {
        InitializeComponent();
    }

    private readonly LauncherMainWindowViewModel _viewModel = default!;
    private readonly IMessenger _messenger = default!;
    private readonly AvaloniaWindowManager _windowManager = default!;
    private readonly LauncherSettingsManager _launcherSettingsManager = default!;
    private readonly ProcessManagerFactory _processManagerFactory = default!;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        if (Design.IsDesignMode)
            return;

        _launcherSettingsManager.LoadSettingsAsync()
            .ContinueWith(x =>
            {
                var config = x.Result ?? new LauncherSettingsModel();
                _viewModel.UseMicrophone = config.UseMicrophone;
                _viewModel.UseWebCamera = config.UseWebCamera;
                _viewModel.SharePrinters = config.SharePrinters;
                _viewModel.MountNpkiFolders = config.MountNpkiFolders;
                _viewModel.MountSpecificFolders = config.MountSpecificFolders;
                _viewModel.DisclaimerAccepted = config.DisclaimerAccepted;

                _viewModel.Folders.Clear();
                foreach (var eachDir in config.Folders)
                    _viewModel.Folders.Add(eachDir);

                var requireAcknowledge = false;
                if (_viewModel.DisclaimerAccepted.HasValue)
                {
                    if ((DateTime.UtcNow - _viewModel.DisclaimerAccepted.Value).TotalDays > 7)
                    {
                        requireAcknowledge = true;
                        _viewModel.DisclaimerAccepted = null;
                    }
                }
                else
                    requireAcknowledge = true;

                if (requireAcknowledge)
                {
                    Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        var window = _windowManager.GetAvaloniaWindow<DisclaimerWindow>();
                        await window.ShowDialog(this);
                        _viewModel.DisclaimerAccepted = DateTime.UtcNow;
                    });
                }
            })
            .SafeFireAndForget();
        base.OnLoaded(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _messenger?.UnregisterAll(this);

        var config = new LauncherSettingsModel
        {
            UseMicrophone = _viewModel.UseMicrophone,
            UseWebCamera = _viewModel.UseWebCamera,
            SharePrinters = _viewModel.SharePrinters,
            MountNpkiFolders = _viewModel.MountNpkiFolders,
            MountSpecificFolders = _viewModel.MountSpecificFolders,
            Folders = _viewModel.Folders.ToArray(),
            DisclaimerAccepted = _viewModel.DisclaimerAccepted,
        };

        _launcherSettingsManager.SaveSettingsAsync(config).SafeFireAndForget();

        base.OnClosed(e);
    }

    void IRecipient<ShowDisclaimerWindowMessage>.Receive(ShowDisclaimerWindowMessage message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var window = _windowManager.GetAvaloniaWindow<DisclaimerWindow>();
            window.ShowDialog(this);
        });
    }

    void IRecipient<AboutButtonMessage>.Receive(AboutButtonMessage message)
    {
        var aboutWindow = _windowManager.GetAvaloniaWindow<AboutWindow>();
        aboutWindow.ShowDialog(this);
    }

    void IRecipient<SponsorButtonMessage>.Receive(SponsorButtonMessage message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!Uri.TryCreate(TableCloth3.Shared.Languages.SharedStrings.GitHubSponsorsUrl, UriKind.Absolute, out var parsedUri) ||
                parsedUri == null)
                return;

            using var process = _processManagerFactory.CreateShellExecuteProcess(parsedUri.AbsoluteUri);
            process.Start();
        });
    }

    void IRecipient<ManageFolderButtonMessage>.Receive(ManageFolderButtonMessage message)
    {
        var folderManageWindow = _windowManager.GetAvaloniaWindow<FolderManageWindow>();

        folderManageWindow.ViewModel.Folders.Clear();
        foreach (var eachDir in message.Folders)
            folderManageWindow.ViewModel.Folders.Add(eachDir);

        folderManageWindow.ShowDialog(this).ContinueWith(x =>
        {
            if (x.IsCompletedSuccessfully)
            {
                _viewModel.Folders.Clear();

                foreach (var eachDir in folderManageWindow.ViewModel.Folders)
                    _viewModel.Folders.Add(eachDir);
            }
        }).SafeFireAndForget();
    }

    void IRecipient<NotifyErrorMessage>.Receive(NotifyErrorMessage message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var messageBoxParam = new MessageBoxStandardParams()
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ContentTitle = LauncherStrings.ErrorTitle,
                ContentMessage = message.FoundException.Message,
                Icon = MsBox.Avalonia.Enums.Icon.Error,
                ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok,
                EnterDefaultButton = MsBox.Avalonia.Enums.ClickEnum.Ok,
                EscDefaultButton = MsBox.Avalonia.Enums.ClickEnum.Cancel,
            };

            MessageBoxManager
                .GetMessageBoxStandard(messageBoxParam)
                .ShowWindowDialogAsync(this)
                .SafeFireAndForget();
        });
    }

    void IRecipient<NotifyWarningsMessage>.Receive(NotifyWarningsMessage message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var messageBoxParam = new MessageBoxStandardParams()
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ContentTitle = LauncherStrings.WarningTitle,
                ContentMessage = string.Join(Environment.NewLine, message.FoundWarnings),
                Icon = MsBox.Avalonia.Enums.Icon.Warning,
                ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok,
                EnterDefaultButton = MsBox.Avalonia.Enums.ClickEnum.Ok,
                EscDefaultButton = MsBox.Avalonia.Enums.ClickEnum.Cancel,
            };

            MessageBoxManager
                .GetMessageBoxStandard(messageBoxParam)
                .ShowWindowDialogAsync(this)
                .SafeFireAndForget();
        });
    }

    void IRecipient<ShowUpdateAvailableMessage>.Receive(ShowUpdateAvailableMessage message)
    {
        Dispatcher.UIThread.Invoke(async () =>
        {
        var result = await MessageBoxManager
  .GetMessageBoxStandard(new MessageBoxStandardParams
  {
     WindowStartupLocation = WindowStartupLocation.CenterOwner,
   ContentTitle = LauncherStrings.NewVersionUpdateNotification,
    ContentMessage = string.Format(LauncherStrings.NewVersionAvailableMessage, message.Version),
           Icon = MsBox.Avalonia.Enums.Icon.Info,
      ButtonDefinitions = ButtonEnum.YesNo,
           EnterDefaultButton = ClickEnum.Yes,
       EscDefaultButton = ClickEnum.No
          })
    .ShowWindowDialogAsync(this);

         if (result == ButtonResult.Yes)
   {
      using var process = _processManagerFactory.CreateShellExecuteProcess(message.ReleaseUrl);
          process.Start();
            }
        });
    }
}
