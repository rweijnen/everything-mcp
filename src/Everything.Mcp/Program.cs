using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Everything.Client;
using Everything.Mcp;
using Everything.Mcp.Configuration;
using Serilog;
using Serilog.Events;
using System.Diagnostics.CodeAnalysis;

var builder = Host.CreateApplicationBuilder(args);

// Re-add configuration sources for appsettings.json support
builder.Configuration.Sources.Clear();
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Configure host options for better shutdown handling
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(10);
});

// Load configuration
var config = new EverythingMcpConfiguration();
BindConfiguration(builder.Configuration.GetSection("EverythingMcp"), config);

// Configure Serilog based on configuration
var loggerConfig = new LoggerConfiguration();

// Parse log level
if (Enum.TryParse<LogEventLevel>(config.Logging.LogLevel, true, out var logLevel))
{
    loggerConfig.MinimumLevel.Is(logLevel);
}
else
{
    loggerConfig.MinimumLevel.Information();
}

// Explicitly disable console logging to prevent MCP protocol interference
// MCP uses stdin/stdout for JSON-RPC communication, so console output breaks the protocol

// Add file logging only if enabled
if (config.Logging.Enabled)
{
    // Expand environment variables in path
    var logPath = Environment.ExpandEnvironmentVariables(config.Logging.LogFilePath);

    // Parse rolling interval
    if (Enum.TryParse<RollingInterval>(config.Logging.RollingInterval, true, out var rollingInterval))
    {
        loggerConfig.WriteTo.File(logPath,
            rollingInterval: rollingInterval,
            retainedFileCountLimit: config.Logging.RetainedFileCountLimit,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
    }
    else
    {
        loggerConfig.WriteTo.File(logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: config.Logging.RetainedFileCountLimit,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");
    }
}

Log.Logger = loggerConfig.CreateLogger();

builder.Services.AddSerilog();

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<EverythingMcpTools>();

// Register configuration
builder.Services.AddSingleton(config);

// Register Everything client directly
builder.Services.Configure<EverythingClientOptions>(options =>
{
    options.EnableAutoRefresh = config.EverythingClient.EnableAutoRefresh;
    options.RefreshInterval = TimeSpan.FromMinutes(config.EverythingClient.RefreshIntervalMinutes);
    options.DefaultTimeoutMs = config.EverythingClient.DefaultTimeoutMs;
});
builder.Services.AddSingleton<IEverythingClient, EverythingClient>();

var app = builder.Build();

// Run the MCP server
await app.RunAsync();

// Helper method to avoid trimming warnings
[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Configuration binding is safe for known types")]
static void BindConfiguration(IConfigurationSection section, EverythingMcpConfiguration config)
{
    section.Bind(config);
}