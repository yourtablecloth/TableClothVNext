using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System.Diagnostics;
using TableCloth3.Shared.Languages;
using TableCloth3.Shared.Services;
using TableCloth3.Spork.Languages;
using TableCloth3.Spork.ViewModels;
using static TableCloth3.Shared.ViewModels.AboutWindowViewModel;
using static TableCloth3.Spork.ViewModels.SporkMainWindowViewModel;
using static TableCloth3.Spork.ViewModels.TableClothAddonItemViewModel;
using static TableCloth3.Spork.ViewModels.TableClothCatalogItemViewModel;

namespace TableCloth3.Spork.Windows;

public partial class SporkMainWindow :
    Window,
    ILoadingFailureNotificationRecipient,
    ICloseButtonRequestRecipient,
    ILaunchSiteRequestRecipient,
    ILaunchAddonRequestRecipient,
    IVisitWebSiteButtonMessageRecipient,
    IVisitGitHubButtonMessageRecipient,
    ICheckUpdateButtonMessageRecipient
{
    [ActivatorUtilitiesConstructor]
    public SporkMainWindow(
        SporkMainWindowViewModel viewModel,
        IMessenger messenger,
        AvaloniaWindowManager windowManager,
        AvaloniaViewModelManager viewModelManager,
        ProcessManagerFactory processManagerFactory)
        : this()
    {
        _viewModel = viewModel;
        _messenger = messenger;
        _windowManager = windowManager;
        _viewModelManager = viewModelManager;
        _processManagerFactory = processManagerFactory;

        _messenger.Register<LoadingFailureNotification>(this);
        _messenger.Register<CloseButtonRequest>(this);
        _messenger.Register<LaunchSiteRequest>(this);
        _messenger.Register<LaunchAddonRequest>(this);
        _messenger.Register<VisitWebSiteButtonMessage>(this);
        _messenger.Register<VisitGitHubButtonMessage>(this);
        _messenger.Register<CheckUpdateButtonMessage>(this);

        DataContext = _viewModel;

        if (!Debugger.IsAttached)
        {
            _splash.IsVisible = true;
        }
    }

    public SporkMainWindow()
        : base()
    {
        InitializeComponent();
    }

    private readonly SporkMainWindowViewModel _viewModel = default!;
    private readonly IMessenger _messenger = default!;
    private readonly AvaloniaWindowManager _windowManager = default!;
    private readonly AvaloniaViewModelManager _viewModelManager = default!;
    private readonly ProcessManagerFactory _processManagerFactory = default!;

    protected override void OnClosed(EventArgs e)
    {
        _messenger.UnregisterAll(this);
        base.OnClosed(e);
    }

    void IRecipient<LoadingFailureNotification>.Receive(LoadingFailureNotification message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                SporkStrings.UnexpectedErrorMessage_Title,
                string.Format(SporkStrings.UnexpectedErrorMessage_Arg0, message.OccurredException.Message),
                ButtonEnum.Ok, MsBox.Avalonia.Enums.Icon.Error);
            msgBox.ShowWindowDialogAsync(this).SafeFireAndForget();
        });
    }

    void IRecipient<CloseButtonRequest>.Receive(CloseButtonRequest message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Close();
        });
    }

    void IRecipient<LaunchSiteRequest>.Receive(LaunchSiteRequest message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var installerWindow = _windowManager.GetAvaloniaWindow<InstallerProgressWindow>();
            if (!string.IsNullOrWhiteSpace(message.TargetUrl))
                installerWindow.ViewModel.TargetUrl = message.TargetUrl;
            else
                installerWindow.ViewModel.TargetUrl = message.ViewModel.TargetUrl;
            foreach (var eachStep in message.ViewModel.Packages)
            {
                var eachVM = _viewModelManager.GetAvaloniaViewModel<InstallerStepItemViewModel>();
                eachVM.ServiceId = message.ViewModel.ServiceId;
                eachVM.PackageName = eachStep.PackageName;
                eachVM.PackageUrl = eachStep.PackageUrl;
                eachVM.PackageArguments = eachStep.PackageArguments;
                eachVM.RequireUserConfirmation = false;
                eachVM.RequireIndirectExecute = false;
                installerWindow.ViewModel.Steps.Add(eachVM);
            }

            var stSessVM = _viewModelManager.GetAvaloniaViewModel<InstallerStepItemViewModel>();
            stSessVM.ServiceId = message.ViewModel.ServiceId;
            stSessVM.PackageName = "AstxConfig";
            stSessVM.LocalFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "AhnLab", "Safe Transaction", "StSess.exe");
            stSessVM.PackageArguments = "/config";
            stSessVM.RequireUserConfirmation = true;
            stSessVM.UserConfirmationText = SporkStrings.AstxConfirmationMessage;
            stSessVM.RequireIndirectExecute = true;
            installerWindow.ViewModel.Steps.Add(stSessVM);

            installerWindow.ShowDialog(this);
        });
    }

    void IRecipient<LaunchAddonRequest>.Receive(LaunchAddonRequest message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            // TODO: Add arguments support
            using var process = _processManagerFactory.CreateShellExecuteProcess(message.ViewModel.TargetUrl/*, message.ViewModel.Arguments*/);
            if (process.Start())
                process.WaitForExit();
        });
    }

    void IRecipient<VisitWebSiteButtonMessage>.Receive(VisitWebSiteButtonMessage message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!Uri.TryCreate(SharedStrings.ProjectWebSiteUrl, UriKind.Absolute, out var parsedUri) ||
                parsedUri == null)
                return;

            using var process = _processManagerFactory.CreateShellExecuteProcess(parsedUri.AbsoluteUri);
            process.Start();
        });
    }

    void IRecipient<VisitGitHubButtonMessage>.Receive(VisitGitHubButtonMessage message)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (!Uri.TryCreate(SharedStrings.GitHubUrl, UriKind.Absolute, out var parsedUri) ||
                parsedUri == null)
                return;

            using var process = _processManagerFactory.CreateShellExecuteProcess(parsedUri.AbsoluteUri);
            process.Start();
        });
    }

    void IRecipient<CheckUpdateButtonMessage>.Receive(CheckUpdateButtonMessage message)
    {
        Dispatcher.UIThread.Invoke(() => {
            // TODO
        });
    }
}
