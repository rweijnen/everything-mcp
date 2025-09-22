using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Everything.Client;
using Everything.Mcp.Configuration;

namespace Everything.Mcp;

/// <summary>
/// Hosted service wrapper for EverythingClient to handle lifecycle properly
/// </summary>
public class EverythingService : IHostedService, IDisposable
{
    private readonly ILogger<EverythingService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly EverythingMcpConfiguration _config;
    private EverythingClient? _everythingClient;
    private bool _disposed = false;

    public EverythingService(
        ILogger<EverythingService> logger,
        ILoggerFactory loggerFactory,
        EverythingMcpConfiguration config)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _config = config;
    }

    public IEverythingClient Client => _everythingClient ?? throw new InvalidOperationException("EverythingClient not initialized");

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Everything service");

        var options = new EverythingClientOptions
        {
            EnableAutoRefresh = _config.EverythingClient.EnableAutoRefresh,
            RefreshInterval = TimeSpan.FromMinutes(_config.EverythingClient.RefreshIntervalMinutes),
            DefaultTimeoutMs = _config.EverythingClient.DefaultTimeoutMs
        };

        _everythingClient = new EverythingClient(Options.Create(options), _loggerFactory.CreateLogger<EverythingClient>());
        _logger.LogInformation("Everything service started and client initialized");

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Everything service");

        // Give ongoing operations a chance to complete
        await Task.Delay(500, cancellationToken);

        _everythingClient?.Dispose();
        _everythingClient = null;

        _logger.LogInformation("Everything service stopped");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _everythingClient?.Dispose();
            _disposed = true;
        }
    }
}