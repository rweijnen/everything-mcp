using Everything.Client;
using Everything.Interop;
using Everything.Mcp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;

var server = new McpServer();
await server.RunAsync();

namespace Everything.Mcp
{
    public class McpServer : IDisposable
    {
        private readonly EverythingClient _client;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILoggerFactory _loggerFactory;

        public McpServer()
        {
            // Create a simple logger that logs to stderr
            _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = _loggerFactory.CreateLogger<EverythingClient>();

            // Create default options
            var options = Options.Create(new EverythingClientOptions());

            _client = new EverythingClient(options, logger);
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task RunAsync()
        {
            await Console.Error.WriteLineAsync("Everything MCP Server starting...");

            try
            {
                string? line;
                while ((line = await Console.In.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
                        if (request != null)
                        {
                            var response = await HandleRequestAsync(request);
                            var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                            await Console.Out.WriteLineAsync(responseJson);
                        }
                    }
                    catch (Exception ex)
                    {
                        await Console.Error.WriteLineAsync($"Error processing request: {ex.Message}");
                        var errorResponse = new McpResponse
                        {
                            Id = null,
                            Error = new McpError
                            {
                                Code = -32603,
                                Message = "Internal error",
                                Data = ex.Message
                            }
                        };
                        var errorJson = JsonSerializer.Serialize(errorResponse, _jsonOptions);
                        await Console.Out.WriteLineAsync(errorJson);
                    }
                }
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"Server error: {ex.Message}");
            }
        }

        private async Task<McpResponse> HandleRequestAsync(McpRequest request)
        {
            return request.Method switch
            {
                "initialize" => HandleInitialize(request),
                "tools/list" => HandleToolsList(request),
                "tools/call" => await HandleToolCallAsync(request),
                _ => new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32601,
                        Message = "Method not found",
                        Data = request.Method
                    }
                }
            };
        }

        private McpResponse HandleInitialize(McpRequest request)
        {
            return new McpResponse
            {
                Id = request.Id,
                Result = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { }
                    },
                    serverInfo = new
                    {
                        name = "everything-mcp",
                        version = "1.0.0"
                    }
                }
            };
        }

        private McpResponse HandleToolsList(McpRequest request)
        {
            var tools = new object[]
            {
                new
                {
                    name = "search_files",
                    description = "Search for files and folders using Everything search engine",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string",
                                description = "Search query (supports Everything syntax like wildcards, operators, etc.)"
                            },
                            path = new
                            {
                                type = "string",
                                description = "Limit search to specific path (optional)"
                            },
                            includeMetadata = new
                            {
                                type = "boolean",
                                description = "Include file metadata (size, dates, attributes) - slower but more detailed",
                                @default = false
                            },
                            maxResults = new
                            {
                                type = "integer",
                                description = "Maximum number of results to return",
                                @default = 100,
                                minimum = 1,
                                maximum = 10000
                            }
                        },
                        required = new[] { "query" }
                    }
                },
                new
                {
                    name = "search_in_project",
                    description = "Search within a project directory (recursive) with smart filtering",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            projectPath = new
                            {
                                type = "string",
                                description = "Project root directory path"
                            },
                            query = new
                            {
                                type = "string",
                                description = "File pattern, name, or content to search for"
                            },
                            fileTypes = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "File extensions to include (e.g., ['.js', '.ts', '.py'])"
                            },
                            excludeDirs = new
                            {
                                type = "array",
                                items = new { type = "string" },
                                description = "Directories to exclude (e.g., ['node_modules', '.git', 'bin'])",
                                @default = new[] { "node_modules", ".git", "bin", "obj", ".vs", ".vscode" }
                            },
                            maxResults = new
                            {
                                type = "integer",
                                description = "Maximum number of results to return",
                                @default = 100,
                                minimum = 1,
                                maximum = 1000
                            }
                        },
                        required = new[] { "projectPath", "query" }
                    }
                },
                new
                {
                    name = "find_executable",
                    description = "Locate executables in PATH or system (like 'where' command)",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new
                            {
                                type = "string",
                                description = "Executable name (with or without .exe extension)"
                            },
                            includeVersionInfo = new
                            {
                                type = "boolean",
                                description = "Include file version metadata if available",
                                @default = false
                            },
                            exactMatch = new
                            {
                                type = "boolean",
                                description = "Exact filename match only (excludes .mui, .pf, etc.)",
                                @default = true
                            }
                        },
                        required = new[] { "name" }
                    }
                }
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = new { tools }
            };
        }

        private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
        {
            if (request.Params?.Arguments == null)
            {
                return new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32602,
                        Message = "Invalid params"
                    }
                };
            }

            var toolName = request.Params.Name;
            var arguments = request.Params.Arguments;

            return toolName switch
            {
                "search_files" => await HandleSearchFilesAsync(request.Id, arguments),
                "search_in_project" => await HandleSearchInProjectAsync(request.Id, arguments),
                "find_executable" => await HandleFindExecutableAsync(request.Id, arguments),
                _ => new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32601,
                        Message = "Tool not found",
                        Data = toolName
                    }
                }
            };
        }

        private async Task<McpResponse> HandleSearchFilesAsync(object? requestId, JsonElement arguments)
        {
            try
            {
                // Parse arguments
                if (!arguments.TryGetProperty("query", out var queryElement))
                {
                    return new McpResponse
                    {
                        Id = requestId,
                        Error = new McpError
                        {
                            Code = -32602,
                            Message = "Missing required parameter: query"
                        }
                    };
                }

                var query = queryElement.GetString();
                if (string.IsNullOrWhiteSpace(query))
                {
                    return new McpResponse
                    {
                        Id = requestId,
                        Error = new McpError
                        {
                            Code = -32602,
                            Message = "Query cannot be empty"
                        }
                    };
                }

                // Handle optional path parameter
                if (arguments.TryGetProperty("path", out var pathElement))
                {
                    var path = pathElement.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        // Add path constraint to query using Everything syntax
                        query = $"{query} path:\"{path}\"";
                    }
                }

                var includeMetadata = arguments.TryGetProperty("includeMetadata", out var metadataElement) &&
                                     metadataElement.GetBoolean();

                var maxResults = arguments.TryGetProperty("maxResults", out var maxElement) ?
                                maxElement.GetUInt32() : 100u;

                // Perform search using appropriate mode
                SearchResult[] results;
                if (includeMetadata)
                {
                    results = await _client.SearchWithMetadataAsync(
                        query,
                        Query2RequestFlags.All,
                        SearchFlags.None,
                        CancellationToken.None);
                }
                else
                {
                    results = await _client.SearchBasicAsync(
                        query,
                        SearchFlags.None,
                        CancellationToken.None);
                }

                // Limit results
                if (results.Length > maxResults)
                {
                    results = results.Take((int)maxResults).ToArray();
                }

                // Format results for MCP
                var mcpResults = results.Select(r => new
                {
                    name = r.Name,
                    path = r.Path,
                    fullPath = r.FullPath,
                    isFolder = r.IsFolder,
                    isDrive = r.IsDrive,
                    size = includeMetadata ? r.Size : null,
                    dateCreated = includeMetadata ? r.DateCreated?.ToString("yyyy-MM-ddTHH:mm:ssZ") : null,
                    dateModified = includeMetadata ? r.DateModified?.ToString("yyyy-MM-ddTHH:mm:ssZ") : null,
                    dateAccessed = includeMetadata ? r.DateAccessed?.ToString("yyyy-MM-ddTHH:mm:ssZ") : null,
                    attributes = includeMetadata ? r.Attributes : null
                }).ToArray();

                return new McpResponse
                {
                    Id = requestId,
                    Result = new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Found {results.Length} results for query: {query}\n\n" +
                                      string.Join("\n", mcpResults.Select(FormatResult))
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    Id = requestId,
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = "Search failed",
                        Data = ex.Message
                    }
                };
            }
        }

        private async Task<McpResponse> HandleSearchInProjectAsync(object? requestId, JsonElement arguments)
        {
            try
            {
                // Parse required arguments
                if (!arguments.TryGetProperty("projectPath", out var projectPathElement))
                {
                    return new McpResponse
                    {
                        Id = requestId,
                        Error = new McpError
                        {
                            Code = -32602,
                            Message = "Missing required parameter: projectPath"
                        }
                    };
                }

                if (!arguments.TryGetProperty("query", out var queryElement))
                {
                    return new McpResponse
                    {
                        Id = requestId,
                        Error = new McpError
                        {
                            Code = -32602,
                            Message = "Missing required parameter: query"
                        }
                    };
                }

                var projectPath = projectPathElement.GetString();
                var query = queryElement.GetString();

                if (string.IsNullOrWhiteSpace(projectPath) || string.IsNullOrWhiteSpace(query))
                {
                    return new McpResponse
                    {
                        Id = requestId,
                        Error = new McpError
                        {
                            Code = -32602,
                            Message = "projectPath and query cannot be empty"
                        }
                    };
                }

                // Parse optional parameters
                var maxResults = arguments.TryGetProperty("maxResults", out var maxElement) ?
                                maxElement.GetUInt32() : 100u;

                var excludeDirs = new[] { "node_modules", ".git", "bin", "obj", ".vs", ".vscode", "target", "build", "__pycache__" };
                if (arguments.TryGetProperty("excludeDirs", out var excludeElement) && excludeElement.ValueKind == JsonValueKind.Array)
                {
                    excludeDirs = excludeElement.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToArray();
                }

                // Build Everything search query
                var searchQuery = new List<string>();

                // Add path constraint
                searchQuery.Add($"path:\"{projectPath}\"");

                // Add file type constraints if specified
                if (arguments.TryGetProperty("fileTypes", out var fileTypesElement) && fileTypesElement.ValueKind == JsonValueKind.Array)
                {
                    var extensions = fileTypesElement.EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString()!)
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToArray();

                    if (extensions.Length > 0)
                    {
                        var extQuery = string.Join(" | ", extensions.Select(ext =>
                            ext.StartsWith('.') ? $"ext:{ext[1..]}" : $"ext:{ext}"));
                        searchQuery.Add($"({extQuery})");
                    }
                }

                // Add exclude directory constraints
                foreach (var excludeDir in excludeDirs)
                {
                    searchQuery.Add($"!path:*{excludeDir}*");
                }

                // Add the actual search query
                searchQuery.Add(query);

                var finalQuery = string.Join(" ", searchQuery);

                // Perform search
                var results = await _client.SearchBasicAsync(
                    finalQuery,
                    SearchFlags.None,
                    CancellationToken.None);

                // Limit results
                if (results.Length > maxResults)
                {
                    results = results.Take((int)maxResults).ToArray();
                }

                // Format results
                var mcpResults = results.Select(r => new
                {
                    name = r.Name,
                    path = r.Path,
                    fullPath = r.FullPath,
                    isFolder = r.IsFolder,
                    relativePath = Path.GetRelativePath(projectPath, r.FullPath)
                }).ToArray();

                return new McpResponse
                {
                    Id = requestId,
                    Result = new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Found {results.Length} results in project '{Path.GetFileName(projectPath)}' for query: {query}\n\n" +
                                      string.Join("\n", mcpResults.Select(FormatResult))
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    Id = requestId,
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = "Project search failed",
                        Data = ex.Message
                    }
                };
            }
        }

        private async Task<McpResponse> HandleFindExecutableAsync(object? requestId, JsonElement arguments)
        {
            try
            {
                // Parse required arguments
                if (!arguments.TryGetProperty("name", out var nameElement))
                {
                    return new McpResponse
                    {
                        Id = requestId,
                        Error = new McpError
                        {
                            Code = -32602,
                            Message = "Missing required parameter: name"
                        }
                    };
                }

                var executableName = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(executableName))
                {
                    return new McpResponse
                    {
                        Id = requestId,
                        Error = new McpError
                        {
                            Code = -32602,
                            Message = "Executable name cannot be empty"
                        }
                    };
                }

                var includeVersionInfo = arguments.TryGetProperty("includeVersionInfo", out var versionElement) &&
                                        versionElement.GetBoolean();

                var exactMatch = !arguments.TryGetProperty("exactMatch", out var exactElement) ||
                                exactElement.GetBoolean(); // Default to true

                // Build search query
                string finalQuery;

                // Search for executable name with .exe extension
                if (!executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (exactMatch)
                    {
                        // Exact match: use regex to match only the exact filename
                        finalQuery = $"regex:^{Regex.Escape(executableName)}\\.exe$";
                    }
                    else
                    {
                        // Broad match: includes related files like .mui, .pf, etc.
                        finalQuery = $"{executableName}.exe";
                    }
                }
                else
                {
                    if (exactMatch)
                    {
                        // Exact match: use regex to match only the exact filename
                        finalQuery = $"regex:^{Regex.Escape(executableName)}$";
                    }
                    else
                    {
                        // Broad match: includes related files
                        finalQuery = executableName;
                    }
                }

                // Perform search
                SearchResult[] results;
                if (includeVersionInfo)
                {
                    results = await _client.SearchWithMetadataAsync(
                        finalQuery,
                        Query2RequestFlags.Name | Query2RequestFlags.Path | Query2RequestFlags.Size | Query2RequestFlags.DateModified,
                        SearchFlags.None,
                        CancellationToken.None);
                }
                else
                {
                    results = await _client.SearchBasicAsync(
                        finalQuery,
                        SearchFlags.None,
                        CancellationToken.None);
                }

                // Format results
                var mcpResults = results.Select(r => new
                {
                    name = r.Name,
                    fullPath = r.FullPath,
                    size = includeVersionInfo ? r.Size : null,
                    dateModified = includeVersionInfo ? r.DateModified?.ToString("yyyy-MM-ddTHH:mm:ssZ") : null,
                    isInPath = IsInSystemPath(r.FullPath)
                }).ToArray();

                return new McpResponse
                {
                    Id = requestId,
                    Result = new
                    {
                        content = new[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Found {results.Length} executable(s) matching '{executableName}':\n\n" +
                                      string.Join("\n", mcpResults.Select(FormatResult))
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    Id = requestId,
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = "Executable search failed",
                        Data = ex.Message
                    }
                };
            }
        }

        private static bool IsInSystemPath(string executablePath)
        {
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? Array.Empty<string>();
            var execDir = Path.GetDirectoryName(executablePath);

            return pathDirs.Any(pathDir =>
                string.Equals(pathDir.TrimEnd('\\'), execDir, StringComparison.OrdinalIgnoreCase));
        }

        private static string FormatResult(object result)
        {
            var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            return json;
        }

        public void Dispose()
        {
            _client?.Dispose();
            _loggerFactory?.Dispose();
        }
    }

    public class McpRequest
    {
        public string Jsonrpc { get; set; } = "2.0";
        public object? Id { get; set; }
        public string Method { get; set; } = "";
        public McpParams? Params { get; set; }
    }

    public class McpParams
    {
        public string? Name { get; set; }
        public JsonElement Arguments { get; set; }
    }

    public class McpResponse
    {
        public string Jsonrpc { get; set; } = "2.0";
        public object? Id { get; set; }
        public object? Result { get; set; }
        public McpError? Error { get; set; }
    }

    public class McpError
    {
        public int Code { get; set; }
        public string Message { get; set; } = "";
        public object? Data { get; set; }
    }
}