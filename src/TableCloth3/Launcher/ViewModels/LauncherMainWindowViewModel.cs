using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Text.Json;
using TableCloth3.Launcher.Services;
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
        WindowsSandboxLauncher windowsSandboxLauncher,
        TableClothCatalogService tableClothCatalogService,
        ProcessManagerFactory processManagerFactory,
        AvaloniaWindowManager windowManager,
        McpServerStatusService mcpServerStatusService,
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
        _mcpServerStatusService = mcpServerStatusService;
        _gitHubUpdateService = gitHubUpdateService;

        // Subscribe to server status change events
        _mcpServerStatusService.ServerStatusChanged += OnServerStatusChanged;
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
    private readonly McpServerStatusService _mcpServerStatusService = default!;
    private readonly GitHubUpdateService _gitHubUpdateService = default!;

    private DispatcherTimer? _statusCheckTimer;

    public sealed record class ShowDisclaimerWindowMessage();

    public interface IShowDisclaimerWindowMessageRecipient : IRecipient<ShowDisclaimerWindowMessage>;

    public sealed record class AboutButtonMessage;

    public interface IAboutButtonMessageRecipient : IRecipient<AboutButtonMessage>;

    public sealed record class CloseButtonMessage;

    public interface ICloseButtonMessageRecipient : IRecipient<CloseButtonMessage>;

    public sealed record class McpServerCloseConfirmationMessage;

    public interface IMcpServerCloseConfirmationMessageRecipient : IRecipient<McpServerCloseConfirmationMessage>;

    public sealed record class ManageFolderButtonMessage(ObservableCollection<string> Folders);

    public interface IManageFolderButtonMessageRecipient : IRecipient<ManageFolderButtonMessage>;

    public sealed record class NotifyErrorMessage(Exception FoundException);

    public interface INotifyErrorMessageRecipient : IRecipient<NotifyErrorMessage>;

    public sealed record class NotifyWarningsMessage(IEnumerable<string> FoundWarnings);

    public interface INotifyWarningsMessageRecipient : IRecipient<NotifyWarningsMessage>;

    public sealed record class CopyMcpConfigMessage();

    public interface ICopyMcpConfigMessageRecipient : IRecipient<CopyMcpConfigMessage>;

    public sealed record class ShowUpdateAvailableMessage(string Version, string ReleaseUrl);

    public interface IShowUpdateAvailableMessageRecipient : IRecipient<ShowUpdateAvailableMessage>;

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

    // MCP server status related properties implemented directly
    private bool _isMcpServerHealthy = false;
    public bool IsMcpServerHealthy
    {
        get => _isMcpServerHealthy;
        set => SetProperty(ref _isMcpServerHealthy, value);
    }

    private string _mcpServerStatusText = "Checking MCP server status...";
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

    // MCP configuration related properties
    public McpServerStatus? CurrentServerStatus { get; private set; }

    private void OnServerStatusChanged(object? sender, ServerStatusChangedEventArgs e)
    {
        // Update status on UI thread
        Dispatcher.UIThread.Post(() => UpdateServerStatus(e.Status));
    }

    private void UpdateServerStatus(McpServerStatus status)
    {
        CurrentServerStatus = status;
        IsMcpServerHealthy = status.IsHealthy;

        // Update Command's CanExecute state
        CopyMcpConfigCommand.NotifyCanExecuteChanged();

        if (status.IsHealthy)
        {
            McpServerStatusText = $"MCP server connected (Port: {status.McpServerPort}, Proxy: {status.YarpProxyPort})";
        }
        else
        {
            var statusParts = new List<string>();

            if (!status.IsMcpServerRunning)
                statusParts.Add("MCP server disconnected");

            if (status.YarpProxyStarting)
                statusParts.Add("Proxy server starting");
            else if (!status.IsYarpProxyRunning)
                statusParts.Add("Proxy server disconnected");

            if (!string.IsNullOrEmpty(status.LastError))
                statusParts.Add($"Error: {status.LastError}");

            McpServerStatusText = statusParts.Any() ? string.Join(", ", statusParts) : "MCP server connection failed";
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
            throw new InvalidOperationException("MCP server status information is not available.");

        // Estimate MCP server executable path based on current running application path
        var currentDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;
        var mcpServerPath = Path.Combine(currentDir, "mcp-server", "dist", "index.js");

        // Generate configuration for Claude Desktop only
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
    private void CloseButton()
    {
        // Show confirmation dialog if MCP server is running
        if (IsMcpServerHealthy && CurrentServerStatus?.IsHealthy == true)
        {
            _messenger.Send<McpServerCloseConfirmationMessage>();
        }
        else
        {
            _messenger.Send<CloseButtonMessage>();
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
            CheckMcpServerStatus(cancellationToken),
            CheckForUpdatesInBackground(cancellationToken),
        ]);

        Loading = false;

        // Periodically check MCP server status (in case server hasn't fully started)
        StartPeriodicStatusCheck();
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
            McpServerStatusText = $"Status check failed: {ex.Message}";
        }
        finally
        {
            McpServerStatusChecking = false;
        }
    }

    private void StartPeriodicStatusCheck()
    {
        // Check more frequently initially (every 10 seconds), then every 30 seconds
        _statusCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };

        var checkCount = 0;
        _statusCheckTimer.Tick += async (sender, e) =>
        {
            await CheckMcpServerStatus();
            checkCount++;

            // Change interval to 30 seconds after 6 checks (1 minute)
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
