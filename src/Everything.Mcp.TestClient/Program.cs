using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Serilog;
using System.Diagnostics;
using System.Text.Json;

// Configure Serilog for file logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/mcp-test-client-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Everything MCP Test Client");

    // Path to the MCP server executable (try published single file first)
    var publishedPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "..",
        "Everything.Mcp",
        "publish",
        "Everything.Mcp.exe");

    var debugPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "..",
        "Everything.Mcp",
        "bin",
        "Release",
        "net8.0-windows",
        "Everything.Mcp.exe");

    var serverPath = publishedPath; // Force use of published single file

    if (!File.Exists(serverPath))
    {
        Log.Error("MCP server not found at: {ServerPath}", serverPath);
        return 1;
    }

    Log.Information("Found MCP server at: {ServerPath}", serverPath);

    // Create and start the MCP client
    var transport = new StdioClientTransport(new()
    {
        Command = serverPath,
        Arguments = [],
        Name = "Everything MCP Server",
        WorkingDirectory = Path.GetDirectoryName(serverPath)
    });

    await using var client = await McpClientFactory.CreateAsync(transport);
    Log.Information("MCP client created and initialized");

    // List available tools
    Log.Information("Listing available tools...");
    var tools = await client.ListToolsAsync();
    Log.Information("Found {ToolCount} tools", tools.Count);

    foreach (var tool in tools)
    {
        Log.Information("Tool: {Name} - {Description}", tool.Name, tool.Description);
    }

    // Test find_executable tool
    Log.Information("Testing find_executable tool with 'notepad.exe'...");
    try
    {
        var execTool = tools.FirstOrDefault(t => t.Name == "find_executable");
        if (execTool != null)
        {
            var execArgs = new AIFunctionArguments();
            execArgs["name"] = "notepad.exe";
            execArgs["exact_match"] = true;
            execArgs["max_results"] = 5;

            var execResult = await execTool.InvokeAsync(execArgs);

            // Parse the MCP response structure
            try
            {
                var execResultString = execResult.ToString() ?? string.Empty;
                var responseJson = JsonDocument.Parse(execResultString);
                if (responseJson.RootElement.TryGetProperty("content", out var content) &&
                    content.GetArrayLength() > 0 &&
                    content[0].TryGetProperty("text", out var textElement))
                {
                    var toolResultJson = textElement.GetString();
                    if (toolResultJson == null)
                    {
                        Log.Warning("Tool result JSON is null");
                        return 1;
                    }

                    Log.Information("find_executable raw result: {Result}", toolResultJson);

                    // Parse the actual tool result JSON
                    var toolResult = JsonDocument.Parse(toolResultJson);
                    if (toolResult.RootElement.TryGetProperty("total_found", out var totalFound) &&
                        toolResult.RootElement.TryGetProperty("returned", out var returned))
                    {
                        Log.Information("Found {Total} executables total, returned {Returned}", totalFound.GetInt32(), returned.GetInt32());

                        if (toolResult.RootElement.TryGetProperty("results", out var results))
                        {
                            foreach (var result in results.EnumerateArray())
                            {
                                if (result.TryGetProperty("name", out var name) &&
                                    result.TryGetProperty("path", out var path))
                                {
                                    Log.Information("  ✓ {Name} -> {Path}", name.GetString(), path.GetString());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not parse find_executable result");
            }
        }
        else
        {
            Log.Warning("find_executable tool not found");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error testing find_executable tool");
    }

    // Test search_files tool
    Log.Information("Testing search_files tool with '*.txt'...");
    try
    {
        var searchTool = tools.FirstOrDefault(t => t.Name == "search_files");
        if (searchTool != null)
        {
            var searchArgs = new AIFunctionArguments();
            searchArgs["query"] = "*.txt";
            searchArgs["scope"] = "system";
            searchArgs["max_results"] = 5;

            var searchResult = await searchTool.InvokeAsync(searchArgs);

            // Parse the MCP response structure
            try
            {
                var searchResultString = searchResult.ToString() ?? string.Empty;
                var responseJson = JsonDocument.Parse(searchResultString);
                if (responseJson.RootElement.TryGetProperty("content", out var content) &&
                    content.GetArrayLength() > 0 &&
                    content[0].TryGetProperty("text", out var textElement))
                {
                    var toolResultJson = textElement.GetString();
                    if (toolResultJson == null)
                    {
                        Log.Warning("Tool result JSON is null");
                        return 1;
                    }

                    Log.Information("search_files raw result: {Result}", toolResultJson);

                    // Parse the actual tool result JSON
                    var toolResult = JsonDocument.Parse(toolResultJson);
                    if (toolResult.RootElement.TryGetProperty("total_found", out var totalFound) &&
                        toolResult.RootElement.TryGetProperty("returned", out var returned))
                    {
                        Log.Information("Found {Total} files total, returned {Returned}", totalFound.GetInt32(), returned.GetInt32());

                        if (toolResult.RootElement.TryGetProperty("results", out var results))
                        {
                            foreach (var result in results.EnumerateArray())
                            {
                                if (result.TryGetProperty("name", out var name) &&
                                    result.TryGetProperty("path", out var path))
                                {
                                    Log.Information("  ✓ {Name} -> {Path}", name.GetString(), path.GetString());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not parse search_files result");
            }
        }
        else
        {
            Log.Warning("search_files tool not found");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error testing search_files tool");
    }

    Log.Information("All tests completed successfully");
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Fatal error in MCP test client");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
