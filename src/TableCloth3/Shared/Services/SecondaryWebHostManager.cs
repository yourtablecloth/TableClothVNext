using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace TableCloth3.Shared.Services;

public sealed class SecondaryWebHostManager : IDisposable
{
    public SecondaryWebHostManager(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // 상태 변경 이벤트
    public event EventHandler<SecondaryWebHostStatusChangedEventArgs>? StatusChanged;

    public async Task<WebApplication?> StartSecondaryWebHost(string targetAddress, CancellationToken cancellationToken = default)
    {
        if (_app != null)
            return _app;

        IsStarting = true;
        OnStatusChanged(SecondaryWebHostStatus.Starting);

        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:29400");
            builder.Services.AddReverseProxy().LoadFromMemory(
                [
                    new RouteConfig
                    {
                        RouteId = "mcp-proxy",
                        ClusterId = "mcp",
                        Match = new RouteMatch { Path = "/{**remainder}" },
                        Transforms =
                        [
                            new Dictionary<string, string>
                            {
                                { "PathPattern", "/{**remainder}" },
                            },
                            new Dictionary<string, string>
                            {
                                { "RequestHeadersCopy", "true" },
                            },
                            new Dictionary<string, string>
                            {
                                { "RequestHeader", "Connection" },
                                { "Set", "Upgrade" },
                            }
                        ]
                    },
                ],
                [
                    new ClusterConfig
                    {
                        ClusterId = "mcp",
                        HttpRequest = new ForwarderRequestConfig
                        {
                            Version = HttpVersion.Version11,
                            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
                        },
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            { "target", new DestinationConfig { Address = targetAddress, } },
                        },
                    },
                ]
            );

            var webApp = builder.Build();
            webApp.MapReverseProxy();
            _app = webApp;

            await webApp.StartAsync(cancellationToken).ConfigureAwait(false);
            _lastException = default;
            IsStarting = false;
            OnStatusChanged(SecondaryWebHostStatus.Started);
            return webApp;
        }
        catch (Exception ex)
        {
            _lastException = ex;
            IsStarting = false;
            OnStatusChanged(SecondaryWebHostStatus.Failed);
            return default;
        }
    }

    private void OnStatusChanged(SecondaryWebHostStatus status)
    {
        StatusChanged?.Invoke(this, new SecondaryWebHostStatusChangedEventArgs(status, _lastException));
    }

    private IConfiguration _configuration = default!;

    private bool _disposed;
    private WebApplication? _app;
    private Exception? _lastException;

    public Exception? LastException => _lastException;
    public bool IsStarting { get; private set; }

    private void Dispose(bool disposing)
    {
        if (!Interlocked.CompareExchange(ref _disposed, true, false))
        {
            if (disposing)
            {
                ((IDisposable?)_app)?.Dispose();
                _app = null;
                OnStatusChanged(SecondaryWebHostStatus.Stopped);
            }
        }
    }

    ~SecondaryWebHostManager()
        => Dispose(false);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

public enum SecondaryWebHostStatus
{
    NotStarted,
    Starting,
    Started,
    Failed,
    Stopped
}

public sealed class SecondaryWebHostStatusChangedEventArgs : EventArgs
{
    public SecondaryWebHostStatusChangedEventArgs(SecondaryWebHostStatus status, Exception? exception = null)
    {
        Status = status;
        Exception = exception;
    }

    public SecondaryWebHostStatus Status { get; }
    public Exception? Exception { get; }
}
