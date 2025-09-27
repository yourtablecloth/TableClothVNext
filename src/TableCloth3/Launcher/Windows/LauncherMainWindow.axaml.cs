using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System.ComponentModel;
using System.Threading;
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
    ICloseButtonMessageRecipient,
    IMcpServerCloseConfirmationMessageRecipient,
    IManageFolderButtonMessageRecipient,
    INotifyErrorMessageRecipient,
    INotifyWarningsMessageRecipient,
    ICopyMcpConfigMessageRecipient,
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
        _messenger.Register<CloseButtonMessage>(this);
        _messenger.Register<McpServerCloseConfirmationMessage>(this);
        _messenger.Register<ManageFolderButtonMessage>(this);
        _messenger.Register<NotifyErrorMessage>(this);
        _messenger.Register<NotifyWarningsMessage>(this);
        _messenger.Register<CopyMcpConfigMessage>(this);
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

    private bool _forceClose = false;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // 강제 닫기가 아니고 MCP 서버가 구동 중이면 종료를 취소하고 확인 다이얼로그를 표시
        if (!_forceClose && _viewModel.IsMcpServerHealthy && _viewModel.CurrentServerStatus?.IsHealthy == true)
        {
            e.Cancel = true;
            
            Dispatcher.UIThread.Post(async () =>
            {
                var messageBoxParam = new MessageBoxStandardParams()
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ContentTitle = "MCP 서버 실행 중",
                    ContentMessage = "MCP 서버가 현재 실행 중이며 Claude Desktop에서 사용 중일 수 있습니다.\n\n서버를 계속 실행하려면 최소화하시거나, 완전히 종료하시겠습니까?",
                    Icon = MsBox.Avalonia.Enums.Icon.Question,
                    ButtonDefinitions = ButtonEnum.OkCancel,
                    EnterDefaultButton = ClickEnum.Cancel,
                };

                // 사용자 정의 버튼을 사용하여 구현
                var result = await MessageBoxManager
                    .GetMessageBoxStandard(new MessageBoxStandardParams
                    {
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        ContentTitle = "MCP 서버 실행 중",
                        ContentMessage = "MCP 서버가 현재 실행 중이며 Claude Desktop에서 사용 중일 수 있습니다.\n\n서버를 계속 실행하려면 '취소'를 선택하여 최소화하거나, '확인'을 선택하여 완전히 종료하시겠습니까?",
                        Icon = MsBox.Avalonia.Enums.Icon.Question,
                        ButtonDefinitions = ButtonEnum.OkCancel,
                        EnterDefaultButton = ClickEnum.Cancel,
                        EscDefaultButton = ClickEnum.Cancel
                    })
                    .ShowWindowDialogAsync(this);

                if (result == ButtonResult.Cancel)
                {
                    // 최소화
                    WindowState = WindowState.Minimized;
                }
                else if (result == ButtonResult.Ok)
                {
                    // 강제 종료 플래그를 설정하고 다시 닫기 시도
                    _forceClose = true;
                    Close();
                }
            });
        }
        
        base.OnClosing(e);
    }

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

    void IRecipient<McpServerCloseConfirmationMessage>.Receive(McpServerCloseConfirmationMessage message)
    {
        Dispatcher.UIThread.Invoke(async () =>
        {
            var result = await MessageBoxManager
                .GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ContentTitle = "MCP 서버 실행 중",
                    ContentMessage = "MCP 서버가 현재 실행 중이며 Claude Desktop에서 사용 중일 수 있습니다.\n\n서버를 계속 실행하려면 '취소'를 선택하여 최소화하거나, '확인'을 선택하여 완전히 종료하시겠습니까?",
                    Icon = MsBox.Avalonia.Enums.Icon.Question,
                    ButtonDefinitions = ButtonEnum.OkCancel,
                    EnterDefaultButton = ClickEnum.Cancel,
                    EscDefaultButton = ClickEnum.Cancel
                })
                .ShowWindowDialogAsync(this);

            if (result == ButtonResult.Cancel)
            {
                // 최소화
                WindowState = WindowState.Minimized;
            }
            else if (result == ButtonResult.Ok)
            {
                // 강제 종료 플래그를 설정하고 프로그램 종료
                _forceClose = true;
                Close();
            }
        });
    }

    void IRecipient<CloseButtonMessage>.Receive(CloseButtonMessage message)
    {
        _forceClose = true;
        Close();
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

    void IRecipient<CopyMcpConfigMessage>.Receive(CopyMcpConfigMessage message)
    {
        if (_viewModel.CurrentServerStatus == null || !_viewModel.CurrentServerStatus.IsHealthy)
            return;

        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
                throw new Exception("클립보드에 접근할 수 없습니다.");

            var config = _viewModel.GenerateMcpConfigJson();
            clipboard.SetTextAsync(config).ContinueWith(task =>
            {
                var originalText = _viewModel.McpServerStatusText;
                if (task.IsCanceled)
                    _viewModel.McpServerStatusText = "설정 복사가 취소되었습니다.";
                else if (task.IsFaulted)
                    _viewModel.McpServerStatusText = "설정 복사 중 오류가 발생했습니다.";
                else if (task.IsCompleted)
                    _viewModel.McpServerStatusText = "Claude Desktop 설정이 클립보드에 복사되었습니다!";
                else
                    _viewModel.McpServerStatusText = "설정 복사에 실패했습니다.";

                // 2초 후 원래 텍스트로 복원
                _ = Task.Delay(TimeSpan.FromSeconds(2d)).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() => _viewModel.McpServerStatusText = originalText);
                });
            }).SafeFireAndForget();
        }
        catch (Exception ex)
        {
            var originalText = _viewModel.McpServerStatusText;
            _viewModel.McpServerStatusText = $"설정 복사 실패: {ex.Message}";

            // 3초 후 원래 텍스트로 복원
            _ = Task.Delay(TimeSpan.FromSeconds(3d)).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() => _viewModel.McpServerStatusText = originalText);
            });
        }
    }

    void IRecipient<ShowUpdateAvailableMessage>.Receive(ShowUpdateAvailableMessage message)
    {
        Dispatcher.UIThread.Invoke(async () =>
        {
            var result = await MessageBoxManager
                .GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ContentTitle = "새 버전 사용 가능",
                    ContentMessage = $"새 버전 {message.Version}이(가) 사용 가능합니다.\n\n다운로드 페이지를 열시겠습니까?",
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
