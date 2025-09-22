using Everything.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Everything.Integration.Tests;

/// <summary>
/// Shared fixture for EverythingClient tests to use a single instance
/// across all tests, preventing multiple window registrations
/// </summary>
public class SharedClientFixture : IDisposable
{
    public EverythingClient Client { get; }
    private readonly ILoggerFactory? _loggerFactory;

    public SharedClientFixture()
    {
        // Use the same shared instance as SharedMcpFixture
        if (SharedTestContext.SharedEverythingClient == null)
        {
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = _loggerFactory.CreateLogger<EverythingClient>();
            var options = Options.Create(new EverythingClientOptions());

            SharedTestContext.SharedEverythingClient = new EverythingClient(options, logger);
        }

        Client = SharedTestContext.SharedEverythingClient;
    }

    public void Dispose()
    {
        // Don't dispose the shared client - it's shared across all tests
        _loggerFactory?.Dispose();
    }
}