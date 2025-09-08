using AsyncAwaitBestPractices;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using TableCloth3.Launcher.Models;
using TableCloth3.Launcher.Services;
using TableCloth3.Launcher.Windows;
using TableCloth3.Shared.Services;
using TableCloth3.Shared.ViewModels;

namespace TableCloth3.Launcher.ViewModels;

public sealed partial class LauncherMainWindowViewModel : BaseViewModel, IDisposable
{
    [ActivatorUtilitiesConstructor]
    public LauncherMainWindowViewModel(
        IMessenger messenger,
        AvaloniaViewModelManager viewModelManager,
        LauncherSettingsManager launcherSettingsManager,
        WindowsSandboxComposer windowsSandboxComposer,
        TableClothCatalogService tableClothCatalogService,
        ProcessManagerFactory processManagerFactory,
        AvaloniaWindowManager windowManager,
        McpServerStatusService mcpServerStatusService)
    {
        _messenger = messenger;
        _viewModelManager = viewModelManager;
        _launcherSettingsManager = launcherSettingsManager;
        _windowsSandboxComposer = windowsSandboxComposer;
        _tableClothCatalogService = tableClothCatalogService;
        _processManagerFactory = processManagerFactory;
        _windowManager = windowManager;
        _mcpServerStatusService = mcpServerStatusService;

        // 서버 상태 변경 이벤트 구독
        _mcpServerStatusService.ServerStatusChanged += OnServerStatusChanged;
    }

    public LauncherMainWindowViewModel() { }

    private readonly IMessenger _messenger = default!;
    private readonly AvaloniaViewModelManager _viewModelManager = default!;
    private readonly LauncherSettingsManager _launcherSettingsManager = default!;
    private readonly WindowsSandboxComposer _windowsSandboxComposer = default!;
    private readonly TableClothCatalogService _tableClothCatalogService = default!;
    private readonly ProcessManagerFactory _processManagerFactory = default!;
    private readonly AvaloniaWindowManager _windowManager = default!;
    private readonly McpServerStatusService _mcpServerStatusService = default!;

    private DispatcherTimer? _statusCheckTimer;

    public sealed record class ShowDisclaimerWindowMessage();

    public interface IShowDisclaimerWindowMessageRecipient : IRecipient<ShowDisclaimerWindowMessage>;

    public sealed record class AboutButtonMessage;

    public interface IAboutButtonMessageRecipient : IRecipient<AboutButtonMessage>;

    public sealed record class CloseButtonMessage;

    public interface ICloseButtonMessageRecipient : IRecipient<CloseButtonMessage>;

    public sealed record class ManageFolderButtonMessage(ObservableCollection<string> Folders);

    public interface IManageFolderButtonMessageRecipient : IRecipient<ManageFolderButtonMessage>;

    public sealed record class NotifyErrorMessage(Exception FoundException);

    public interface INotifyErrorMessageRecipient : IRecipient<NotifyErrorMessage>;

    public sealed record class NotifyWarningsMessage(IEnumerable<string> FoundWarnings);

    public interface INotifyWarningsMessageRecipient : IRecipient<NotifyWarningsMessage>;

    public sealed record class CopyMcpConfigMessage();

    public interface ICopyMcpConfigMessageRecipient : IRecipient<CopyMcpConfigMessage>;

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

    // MCP 서버 상태 관련 프로퍼티들을 직접 구현
    private bool _isMcpServerHealthy = false;
    public bool IsMcpServerHealthy
    {
        get => _isMcpServerHealthy;
        set => SetProperty(ref _isMcpServerHealthy, value);
    }

    private string _mcpServerStatusText = "MCP 서버 상태 확인 중...";
    public string McpServerStatusText
    {
        get => _mcpServerStatusText;
        set => SetProperty(ref _mcpServerStatusText, value);
    }

    private bool _mcpServerStatusChecking = false;
    public bool McpServerStatusChecking
    {
        get => _mcpServerStatusChecking;
        set => SetProperty(ref _mcpServerStatusChecking, value);
    }

    // MCP 설정 관련 프로퍼티
    public McpServerStatus? CurrentServerStatus { get; private set; }

    private void OnServerStatusChanged(object? sender, ServerStatusChangedEventArgs e)
    {
        // UI 스레드에서 상태 업데이트
        Dispatcher.UIThread.Post(() => UpdateServerStatus(e.Status));
    }

    private void UpdateServerStatus(McpServerStatus status)
    {
        CurrentServerStatus = status;
        IsMcpServerHealthy = status.IsHealthy;

        // Command의 CanExecute 상태 업데이트
        CopyMcpConfigCommand.NotifyCanExecuteChanged();

        if (status.IsHealthy)
        {
            McpServerStatusText = $"MCP 서버 연결됨 (포트: {status.McpServerPort}, 프록시: {status.YarpProxyPort})";
        }
        else
        {
            var statusParts = new List<string>();

            if (!status.IsMcpServerRunning)
                statusParts.Add("MCP 서버 미연결");

            if (status.YarpProxyStarting)
                statusParts.Add("프록시 서버 시작 중");
            else if (!status.IsYarpProxyRunning)
                statusParts.Add("프록시 서버 미연결");

            if (!string.IsNullOrEmpty(status.LastError))
                statusParts.Add($"오류: {status.LastError}");

            McpServerStatusText = statusParts.Any() ? string.Join(", ", statusParts) : "MCP 서버 연결 실패";
        }
    }

    [RelayCommand(CanExecute = nameof(CanCopyMcpConfig))]
    private void CopyMcpConfig()
        => _messenger.Send<CopyMcpConfigMessage>();

    private bool CanCopyMcpConfig()
        => CurrentServerStatus?.IsHealthy == true;

    public string GenerateMcpConfigJson()
    {
        if (CurrentServerStatus == null)
            throw new InvalidOperationException("MCP 서버 상태 정보가 없습니다.");

        // 현재 실행 중인 애플리케이션 경로 기준으로 MCP 서버 실행 파일 경로 추정
        var currentDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
        var mcpServerPath = Path.Combine(currentDir, "mcp-server", "dist", "index.js");
        
        // Claude Desktop용 설정만 생성
        var config = new
        {
            mcpServers = new Dictionary<string, object>
            {
                ["tablecloth"] = new
                {
                    command = "node",
                    args = new[] { mcpServerPath },
                    env = new Dictionary<string, string>
                    {
                        ["TABLECLOTH_PROXY_URL"] = $"http://localhost:{CurrentServerStatus.YarpProxyPort}"
                    }
                }
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(config, options);
    }

    [RelayCommand]
    private void AboutButton()
        => _messenger.Send<AboutButtonMessage>();

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LaunchButton(CancellationToken cancellationToken = default)
    {
        try
        {
            var processList = Process.GetProcesses().Select(x => x.ProcessName);
            var lookupList = new List<string> { "WindowsSandbox", "WindowsSandboxRemoteSession", "WindowsSandboxServer", };
            foreach (var eachProcessName in processList)
                if (lookupList.Contains(eachProcessName, StringComparer.OrdinalIgnoreCase))
                    throw new Exception("Only one Windows Sandbox session allowed.");

            var warnings = new List<string>();
            var config = await _launcherSettingsManager.LoadSettingsAsync(cancellationToken).ConfigureAwait(false);
            var wsbPath = await _windowsSandboxComposer.GenerateWindowsSandboxProfileAsync(
                this, warnings, cancellationToken).ConfigureAwait(false);

            if (warnings.Any())
                _messenger.Send<NotifyWarningsMessage>(new NotifyWarningsMessage(warnings));

            var windowsSandboxExecPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsSandbox.exe");

            using var process = _processManagerFactory.CreateCmdShellProcess(windowsSandboxExecPath, wsbPath);

            if (process.Start())
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _messenger.Send<NotifyErrorMessage>(new NotifyErrorMessage(ex));
        }
    }

    [RelayCommand]
    private void CloseButton()
        => _messenger.Send<CloseButtonMessage>();

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
            CheckMcpServerStatus(cancellationToken),
        ]);

        Loading = false;

        // MCP 서버 상태를 주기적으로 확인 (서버가 완전히 시작되지 않았을 경우를 대비)
        StartPeriodicStatusCheck();
    }

    [RelayCommand]
    private async Task CheckMcpServerStatus(CancellationToken cancellationToken = default)
    {
        if (McpServerStatusChecking)
            return;

        McpServerStatusChecking = true;

        try
        {
            var status = await _mcpServerStatusService.CheckStatusAsync(cancellationToken);
            UpdateServerStatus(status);
        }
        catch (Exception ex)
        {
            IsMcpServerHealthy = false;
            McpServerStatusText = $"상태 확인 실패: {ex.Message}";
        }
        finally
        {
            McpServerStatusChecking = false;
        }
    }

    private void StartPeriodicStatusCheck()
    {
        // 서버 시작 초기에는 더 자주 확인 (10초마다), 이후 30초마다 확인
        _statusCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };

        var checkCount = 0;
        _statusCheckTimer.Tick += async (sender, e) =>
        {
            await CheckMcpServerStatus();
            checkCount++;

            // 6번 확인 후(1분 후) 간격을 30초로 변경
            if (checkCount >= 6)
            {
                _statusCheckTimer.Interval = TimeSpan.FromSeconds(30);
            }
        };

        _statusCheckTimer.Start();
    }

    public void Dispose()
    {
        if (_mcpServerStatusService != null)
        {
            _mcpServerStatusService.ServerStatusChanged -= OnServerStatusChanged;
        }

        _statusCheckTimer?.Stop();
        _statusCheckTimer = null;
    }
}
