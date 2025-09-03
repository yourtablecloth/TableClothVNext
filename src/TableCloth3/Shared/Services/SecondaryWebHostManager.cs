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

    public async Task<WebApplication?> StartSecondaryWebHost(string targetAddress, CancellationToken cancellationToken = default)
    {
        if (_app != null)
            return _app;

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
            return webApp;
        }
        catch (Exception ex)
        {
            _lastException = ex;
            return default;
        }
    }

    private IConfiguration _configuration = default!;

    private bool _disposed;
    private WebApplication? _app;
    private Exception? _lastException;

    public Exception? LastException => _lastException;

    private void Dispose(bool disposing)
    {
        if (!Interlocked.CompareExchange(ref _disposed, true, false))
        {
            if (disposing)
            {
                ((IDisposable?)_app)?.Dispose();
                _app = null;
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
