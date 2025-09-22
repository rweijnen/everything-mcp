using Everything.Client;
using Everything.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Everything.Integration.Tests;

/// <summary>
/// Shared fixture that provides a single MCP instance across all tests,
/// mimicking how the real MCP server operates with a single instance
/// handling all requests sequentially.
/// </summary>
public class SharedMcpFixture : IDisposable
{
    public EverythingMcpTools MpcTools { get; }
    public ServiceProvider ServiceProvider { get; }

    public SharedMcpFixture()
    {
        // Set up dependency injection like the MCP server
        var services = new ServiceCollection();

        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

        services.Configure<EverythingClientOptions>(options =>
        {
            options.EnableAutoRefresh = false;
            options.RefreshInterval = TimeSpan.FromMinutes(5);
            options.DefaultTimeoutMs = 10000;
        });

        // Use a static singleton to ensure only one EverythingClient exists across all test classes
        services.AddSingleton<EverythingClient>(provider =>
        {
            if (SharedTestContext.SharedEverythingClient == null)
            {
                var logger = provider.GetRequiredService<ILogger<EverythingClient>>();
                var options = provider.GetRequiredService<IOptions<EverythingClientOptions>>();
                SharedTestContext.SharedEverythingClient = new EverythingClient(options, logger);
            }
            return SharedTestContext.SharedEverythingClient;
        });
        services.AddSingleton<IEverythingClient>(provider => provider.GetRequiredService<EverythingClient>());

        ServiceProvider = services.BuildServiceProvider();

        var everythingClient = ServiceProvider.GetRequiredService<EverythingClient>();
        var logger = ServiceProvider.GetRequiredService<ILogger<EverythingMcpTools>>();

        MpcTools = new EverythingMcpTools(everythingClient, logger);
    }

    public void Dispose()
    {
        // Don't dispose - we're using a static shared instance across all tests
        // ServiceProvider?.Dispose();
    }
}