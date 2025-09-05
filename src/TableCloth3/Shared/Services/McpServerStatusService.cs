using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace TableCloth3.Shared.Services;

public sealed class McpServerStatusService
{
    private readonly IHost _host;
    private readonly SecondaryWebHostManager _secondaryWebHostManager;
    
    // 서버 상태 변경 이벤트
    public event EventHandler<ServerStatusChangedEventArgs>? ServerStatusChanged;

    public McpServerStatusService(IHost host, SecondaryWebHostManager secondaryWebHostManager)
    {
        _host = host;
        _secondaryWebHostManager = secondaryWebHostManager;
        
        // SecondaryWebHostManager의 상태 변경을 구독
        _secondaryWebHostManager.StatusChanged += OnSecondaryWebHostStatusChanged;
    }

    private void OnSecondaryWebHostStatusChanged(object? sender, SecondaryWebHostStatusChangedEventArgs e)
    {
        // YARP 상태 변경 시 전체 상태 확인 트리거
        _ = Task.Run(async () =>
        {
            var status = await CheckStatusAsync();
            ServerStatusChanged?.Invoke(this, new ServerStatusChangedEventArgs(status));
        });
    }

    public async Task<McpServerStatus> CheckStatusAsync(CancellationToken cancellationToken = default)
    {
        var status = new McpServerStatus();

        try
        {
            // MCP 서버 포트 확인
            status.McpServerPort = GetMcpServerPort();
            status.IsMcpServerRunning = status.McpServerPort.HasValue && await IsPortActiveAsync(status.McpServerPort.Value, cancellationToken);

            // YARP 프록시 서버 상태 확인 (포트 29400)
            status.YarpProxyPort = 29400;
            status.IsYarpProxyRunning = await IsPortActiveAsync(status.YarpProxyPort, cancellationToken);
            status.YarpProxyStarting = _secondaryWebHostManager.IsStarting;

            // 전체 상태 계산
            status.IsHealthy = status.IsMcpServerRunning && status.IsYarpProxyRunning;
            status.LastCheckTime = DateTime.Now;

            if (_secondaryWebHostManager.LastException != null)
            {
                status.LastError = _secondaryWebHostManager.LastException.Message;
                status.IsHealthy = false;
            }
        }
        catch (Exception ex)
        {
            status.IsHealthy = false;
            status.LastError = ex.Message;
            status.LastCheckTime = DateTime.Now;
        }

        return status;
    }

    private int? GetMcpServerPort()
    {
        var server = _host.Services.GetRequiredService<IServer>();
        var serverAddressesFeature = server.Features.GetRequiredFeature<IServerAddressesFeature>();
        var address = serverAddressesFeature.Addresses.FirstOrDefault();
        if (!string.IsNullOrEmpty(address) && Uri.TryCreate(address, UriKind.Absolute, out var uri))
            return uri.Port;
        return default;
    }

    private static async Task<bool> IsPortActiveAsync(int port, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port, cancellationToken);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }
}

public sealed class McpServerStatus
{
    public bool IsHealthy { get; set; }
    public bool IsMcpServerRunning { get; set; }
    public bool IsYarpProxyRunning { get; set; }
    public bool YarpProxyStarting { get; set; }
    public int? McpServerPort { get; set; }
    public int YarpProxyPort { get; set; }
    public DateTime LastCheckTime { get; set; }
    public string? LastError { get; set; }
}

public sealed class ServerStatusChangedEventArgs : EventArgs
{
    public ServerStatusChangedEventArgs(McpServerStatus status)
    {
        Status = status;
    }

    public McpServerStatus Status { get; }
}