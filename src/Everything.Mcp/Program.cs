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
    .AddMcpServer(options =>
    {
        options.ServerInstructions = """
            This server provides instant file search across the entire Windows filesystem
            using the Everything search engine (indexed, sub-millisecond lookups).

            When to use these tools vs alternatives:
            - ls/dir: listing a single directory non-recursively (low overhead, small output)
            - Glob: matching file patterns within the current project tree
            - These tools: recursive searches, system-wide searches, finding files when
              the location is unknown, locating executables, or any search across large
              directory trees. Use instead of `where`, `which`, `find`, `ls -R`,
              `dir /s`, or `Get-ChildItem -Recurse`.
            - Grep: searching file *contents* (Everything does not search inside files)

            Paths are automatically normalized to Windows format, so both
            Unix-style (/c/Users/me) and Windows-style (C:\Users\me) paths work
            in parameters. However, path: operators inside raw query strings
            must use Windows backslash format (e.g. path:C:\Users\me).

            To minimize token usage, set max_results to the smallest useful value and
            omit include_metadata unless file sizes or dates are actually needed.
            """;
    })
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
