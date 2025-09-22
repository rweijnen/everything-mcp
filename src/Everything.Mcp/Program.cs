using Everything.Client;
using Everything.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Configure Serilog for file logging only (never to console to keep MCP protocol clean)
// Ensure logs directory exists
var logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logsDir);

// Configure Serilog as the main logger
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

// Test log to verify logging is working
Log.Information("Everything MCP Server starting up at {Timestamp}", DateTime.Now);

// Clear default console logging to keep stdout/stderr clean for MCP protocol
builder.Logging.ClearProviders();
builder.Logging.AddSerilog();

// Register Everything client with default options
builder.Services.AddSingleton<IEverythingClient, EverythingClient>();

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<EverythingMcpTools>();

try
{
    var host = builder.Build();
    await host.RunAsync();
}
finally
{
    Log.CloseAndFlush();
}
