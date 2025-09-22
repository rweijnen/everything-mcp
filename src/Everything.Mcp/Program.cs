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
builder.Configuration.GetSection("EverythingMcp").Bind(config);

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
    .WithToolsFromAssembly();

// Register configuration
builder.Services.AddSingleton(config);

// Register Everything service for proper lifecycle management
builder.Services.AddSingleton<EverythingService>();
builder.Services.AddSingleton<IEverythingClient>(provider => provider.GetRequiredService<EverythingService>().Client);
builder.Services.AddHostedService<EverythingService>(provider => provider.GetRequiredService<EverythingService>());

var app = builder.Build();

// Everything client will be initialized by the hosted service
// Log.Information("MCP server configured and ready to start"); // Commented to prevent MCP protocol interference

// Ensure graceful shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    // Log.Information("MCP server started and ready for requests"); // Commented to prevent MCP protocol interference
});

lifetime.ApplicationStopping.Register(() =>
{
    // Log.Information("MCP server shutting down"); // Commented to prevent MCP protocol interference
});

// Run the MCP server
await app.RunAsync();