using Everything.Client;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Everything.Mcp.Tools;

/// <summary>
/// MCP tools for Everything file search engine integration.
/// Provides 6 specialized search tools for fast file and folder discovery.
/// </summary>
/// <remarks>
/// This class implements Model Context Protocol (MCP) tools that expose Everything Search Engine
/// functionality to AI assistants like Claude. Each method is decorated with [McpServerTool]
/// to make it discoverable and callable via the MCP JSON-RPC protocol.
///
/// Available Tools:
/// - search_files: General file/folder search with Everything syntax
/// - search_in_project: Project-scoped search with smart filtering
/// - find_executable: Executable location with exact/broad matching
/// - find_source_files: Source code search by programming language
/// - search_recent_files: Time-based file search with metadata
/// - find_config_files: Configuration file discovery with grouping
///
/// All tools support Everything's powerful search syntax including:
/// - Wildcards: *.txt, test*.doc
/// - Boolean operators: file1|file2, !exclude
/// - Size filters: size:>1MB, size:10KB..1MB
/// - Date filters: dm:today, dc:2023
/// - Path operators: path:C:\\temp
/// - Regular expressions: regex:pattern
/// </remarks>
internal class EverythingMcpTools
{
    private readonly IEverythingClient _everythingClient;
    private readonly ILogger<EverythingMcpTools> _logger;

    /// <summary>
    /// Initializes a new instance of the EverythingMcpTools class.
    /// </summary>
    /// <param name="everythingClient">The Everything client for search operations.</param>
    /// <param name="logger">Logger for diagnostic information and debugging.</param>
    public EverythingMcpTools(IEverythingClient everythingClient, ILogger<EverythingMcpTools> logger)
    {
        _everythingClient = everythingClient;
        _logger = logger;
    }

    /// <summary>
    /// Searches for files and folders using Everything's powerful search syntax.
    /// This is the most flexible search tool supporting all Everything features.
    /// </summary>
    /// <param name="query">Search query using Everything syntax (wildcards, regex, boolean operators, etc.).</param>
    /// <param name="scope">Search scope limiting where to search ('current', 'recursive', 'path:/folder', 'system').</param>
    /// <param name="include_metadata">Whether to include file metadata like size and dates (impacts performance).</param>
    /// <param name="max_results">Maximum number of results to return (default: 100).</param>
    /// <returns>JSON object containing search results with metadata based on include_metadata setting.</returns>
    /// <remarks>
    /// Scope options:
    /// - 'current': Search only in current directory
    /// - 'recursive': Search current directory and subdirectories
    /// - 'path:/some/folder': Search within specified folder
    /// - 'system': Search entire system
    ///
    /// Query examples:
    /// - "*.txt" - All text files
    /// - "test*.doc|*.docx" - Word documents starting with 'test'
    /// - "size:>1MB *.pdf" - PDF files larger than 1MB
    /// - "dm:today" - Files modified today
    /// - "!temp" - Exclude files/folders with 'temp' in name
    /// </remarks>
    [McpServerTool]
    [Description("Instantly search for files and folders by name using an indexed database. Preferred over `where`, `find`, `dir /s`, or `ls -R` for recursive or system-wide searches. Supports wildcards, regex, boolean operators, size and date filters.")]
    public async Task<object> search_files(
        [Description("Search query with Everything syntax: wildcards (*.cs), regex (regex:pattern), boolean (!exclude, file1|file2), size (size:>1MB), etc.")] string query,
        [Description("Search scope: 'current' (default), 'recursive', 'path:C:\\\\folder', or 'system' for system-wide")] string scope = "current",
        [Description("Include metadata like size, dates (default: false)")] bool include_metadata = false,
        [Description("Maximum number of results (default: 100)")] int max_results = 100)
    {
        try
        {
            // Build the scoped query based on the scope parameter
            string scopedQuery = BuildScopedQuery(query, scope);
            _logger.LogDebug("Original query: {Query}, Scope: {Scope}, Scoped query: {ScopedQuery}", query, scope, scopedQuery);

            var results = include_metadata
                ? await _everythingClient.SearchWithMetadataAsync(scopedQuery)
                : await _everythingClient.SearchBasicAsync(scopedQuery);

            var limited = results.Take(max_results).ToList();
            var mapped = limited.Select(r => BuildResultObject(r, include_metadata)).ToList();
            return BuildResponse(results.Length, limited, mapped);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while searching files with query: {Query}", query);
            return new Dictionary<string, object>
            {
                ["total_found"] = 0,
                ["returned"] = 0,
                ["error"] = "Service is shutting down, please try again",
                ["results"] = new List<object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching files with query: {Query}, scope: {Scope}", query, scope);
            throw;
        }
    }

    [McpServerTool]
    [Description("Instantly search for files by name within a project folder tree. Use instead of `find` or `Get-ChildItem -Recurse` for name-based file discovery.")]
    public async Task<object> search_in_project(
        [Description("Project folder path")] string project_path,
        [Description("Search pattern (e.g., *.cs, test*.txt)")] string pattern,
        [Description("Include metadata like size, dates (default: false)")] bool include_metadata = false,
        [Description("Maximum number of results (default: 100)")] int max_results = 100)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(NormalizeToWindowsPath(project_path)).TrimEnd('\\', '/');
            var query = $"path:\"{normalizedPath}\" {pattern}";

            var results = include_metadata
                ? await _everythingClient.SearchWithMetadataAsync(query)
                : await _everythingClient.SearchBasicAsync(query);

            var limited = results.Take(max_results).ToList();
            var mapped = limited.Select(r => BuildResultObject(r, include_metadata, normalizedPath)).ToList();
            return BuildResponse(results.Length, limited, mapped);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while searching in project {ProjectPath} with pattern: {Pattern}", project_path, pattern);
            return new Dictionary<string, object>
            {
                ["total_found"] = 0,
                ["returned"] = 0,
                ["error"] = "Service is shutting down, please try again",
                ["results"] = new List<object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching in project {ProjectPath} with pattern: {Pattern}", project_path, pattern);
            throw;
        }
    }

    /// <summary>
    /// Finds executable files by name with intelligent matching logic.
    /// Supports exact matching, wildcard patterns, and auto-detection of user intent.
    /// </summary>
    /// <param name="name">Executable name to search for (with or without extension).</param>
    /// <param name="exact_match">Whether to force exact filename matching.</param>
    /// <param name="max_results">Maximum number of results to return.</param>
    /// <returns>JSON object containing executable search results with paths.</returns>
    /// <remarks>
    /// Smart matching logic:
    /// - "notepad" → searches for notepad.exe, notepad.bat, notepad.cmd, notepad.ps1
    /// - "notepad.exe" → exact match for notepad.exe only
    /// - "note*" → wildcard pattern as specified
    /// - exact_match=true → forces exact filename matching
    ///
    /// Searches for common executable extensions: .exe, .bat, .cmd, .ps1
    /// Useful for quickly locating programs, scripts, and system utilities.
    /// </remarks>
    [McpServerTool]
    [Description("Instantly locate executables (.exe, .bat, .cmd, .ps1) system-wide. Use instead of `where`, `which`, or `Get-Command`.")]
    public async Task<object> find_executable(
        [Description("Executable name. Use 'notepad' for variations, 'notepad.exe' for exact match, 'note*' for wildcards")] string name,
        [Description("Force exact match (true) or auto-detect from input (false, default)")] bool exact_match = false,
        [Description("Maximum number of results (default: 50)")] int max_results = 50)
    {
        _logger.LogInformation("find_executable called: name={Name}, exact={Exact}, max={Max}", name, exact_match, max_results);
        try
        {
            string query;

            // Smart logic: detect user intent from the input
            bool hasWildcards = name.Contains('*') || name.Contains('?');
            bool hasExtension = Path.HasExtension(name);
            bool shouldBeExact = exact_match || (hasExtension && !hasWildcards);

            if (hasWildcards)
            {
                // User provided wildcards - use exactly as specified
                query = name;
            }
            else if (shouldBeExact)
            {
                // Specific filename (like "notepad.exe") or explicit exact_match - search exactly
                var exactName = hasExtension ? name : $"{name}.exe";
                query = $"exact:\"{exactName}\""; // Use Everything's exact filename syntax
            }
            else
            {
                // Generic name (like "notepad") - search for variations
                var baseName = Path.GetFileNameWithoutExtension(name);
                query = $"{baseName}*.exe|{baseName}*.bat|{baseName}*.cmd|{baseName}*.ps1";
            }

            _logger.LogDebug("Executing Everything search with query: {Query}", query);
            var results = await _everythingClient.SearchBasicAsync(query);
            _logger.LogDebug("Search returned {Count} results", results.Length);

            var limited = results.Take(max_results).ToList();
            _logger.LogDebug("Limited to {Count} results", limited.Count);

            var mapped = limited.Select(r => BuildResultObject(r, false)).ToList();
            return BuildResponse(results.Length, limited, mapped);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while finding executable: {Name}", name);
            return new Dictionary<string, object>
            {
                ["total_found"] = 0,
                ["returned"] = 0,
                ["error"] = "Service is shutting down, please try again",
                ["results"] = new List<object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding executable: {Name}", name);
            throw;
        }
    }

    [McpServerTool]
    [Description("Instantly find source code files by name across common programming languages. Use instead of shell glob searches like `find -name '*.cs'`.")]
    public async Task<object> find_source_files(
        [Description("Base filename to search for")] string filename,
        [Description("Additional file extensions (comma-separated, optional)")] string? extensions = null,
        [Description("Include metadata like size, dates (default: false)")] bool include_metadata = false,
        [Description("Maximum number of results (default: 100)")] int max_results = 100)
    {
        try
        {
            var defaultExtensions = new[] { ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".c", ".h", ".hpp", ".go", ".rs", ".php", ".rb", ".swift", ".kt" };
            var allExtensions = extensions?.Split(',').Select(e => e.Trim()).ToArray() ?? defaultExtensions;

            var baseName = Path.GetFileNameWithoutExtension(filename);
            var queries = allExtensions.Select(ext => $"{baseName}*{ext}").ToArray();
            var query = string.Join("|", queries);

            var results = include_metadata
                ? await _everythingClient.SearchWithMetadataAsync(query)
                : await _everythingClient.SearchBasicAsync(query);

            var limited = results.Take(max_results).ToList();
            var mapped = limited.Select(r => BuildResultObject(r, include_metadata)).ToList();
            return BuildResponse(results.Length, limited, mapped);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while finding source files: {Filename}", filename);
            return new Dictionary<string, object>
            {
                ["total_found"] = 0,
                ["returned"] = 0,
                ["error"] = "Service is shutting down, please try again",
                ["results"] = new List<object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding source files: {Filename}", filename);
            throw;
        }
    }

    [McpServerTool]
    [Description("Find recently modified files within a time window. Use instead of `find -mtime` or sorting directory listings by date.")]
    public async Task<object> search_recent_files(
        [Description("Time period in hours (default: 24)")] int hours = 24,
        [Description("File pattern to filter by (optional, e.g., *.cs)")] string? pattern = null,
        [Description("Include metadata like size, dates (default: true)")] bool include_metadata = true,
        [Description("Maximum number of results (default: 50)")] int max_results = 50)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddHours(-hours);
            var dateFilter = $"dm:{cutoffDate:yyyy-MM-dd}";

            var query = string.IsNullOrEmpty(pattern) ? dateFilter : $"{pattern} {dateFilter}";

            var results = include_metadata
                ? await _everythingClient.SearchWithMetadataAsync(query)
                : await _everythingClient.SearchBasicAsync(query);

            var sortedResults = results
                .Where(r => r.DateModified.HasValue)
                .OrderByDescending(r => r.DateModified)
                .Take(max_results)
                .ToList();

            var mapped = sortedResults.Select(r => BuildRecentResultObject(r, include_metadata)).ToList();
            var response = BuildResponse(results.Length, sortedResults, mapped);
            response["hours_back"] = hours;
            return response;
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while searching recent files");
            return new Dictionary<string, object>
            {
                ["total_found"] = 0,
                ["returned"] = 0,
                ["error"] = "Service is shutting down, please try again",
                ["results"] = new List<object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching recent files");
            throw;
        }
    }

    [McpServerTool]
    [Description("Find configuration files (json, xml, yaml, ini, toml, config) in a project or system-wide.")]
    public async Task<object> find_config_files(
        [Description("Project folder path (optional, searches everywhere if not specified)")] string? project_path = null,
        [Description("Include metadata like size, dates (default: false)")] bool include_metadata = false,
        [Description("Maximum number of results (default: 100)")] int max_results = 100)
    {
        try
        {
            var configExtensions = new[] { "*.json", "*.xml", "*.yaml", "*.yml", "*.ini", "*.config", "*.toml", "*.properties" };
            var configNames = new[] { "web.config", "app.config", "appsettings.json", "package.json", "tsconfig.json", ".env", ".gitignore", "Dockerfile" };

            var extensionQueries = configExtensions;
            var nameQueries = configNames;

            var allQueries = extensionQueries.Concat(nameQueries);

            string query;
            if (!string.IsNullOrEmpty(project_path))
            {
                var normalizedPath = Path.GetFullPath(NormalizeToWindowsPath(project_path)).TrimEnd('\\', '/');
                var patternQuery = string.Join("|", allQueries);
                query = $"path:\"{normalizedPath}\" {patternQuery}";
            }
            else
            {
                query = string.Join("|", allQueries);
            }

            var results = include_metadata
                ? await _everythingClient.SearchWithMetadataAsync(query)
                : await _everythingClient.SearchBasicAsync(query);

            var limited = results.Take(max_results).ToList();
            var relativeTo = !string.IsNullOrEmpty(project_path) ? project_path : null;
            var mapped = limited.Select(r => BuildResultObject(r, include_metadata, relativeTo)).ToList();
            return BuildResponse(results.Length, limited, mapped);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while finding config files in: {ProjectPath}", project_path);
            return new Dictionary<string, object>
            {
                ["total_found"] = 0,
                ["returned"] = 0,
                ["error"] = "Service is shutting down, please try again",
                ["results"] = new List<object>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding config files in: {ProjectPath}", project_path);
            throw;
        }
    }

    /// <summary>
    /// Builds a lean result object for a single search result, omitting null metadata fields.
    /// </summary>
    private static object BuildResultObject(Everything.Interop.SearchResult r, bool includeMetadata, string? relativeTo = null)
    {
        var dict = new Dictionary<string, object?>
        {
            ["name"] = r.Name,
            ["path"] = r.Path
        };

        if (relativeTo != null)
            dict["relative_path"] = Path.GetRelativePath(relativeTo, r.Path);

        if (includeMetadata)
        {
            if (r.Size.HasValue)
                dict["size"] = r.Size.Value;
            if (r.DateModified.HasValue)
                dict["date_modified"] = r.DateModified.Value.ToString("yyyy-MM-dd HH:mm:ss");
            if (r.DateCreated.HasValue)
                dict["date_created"] = r.DateCreated.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return dict;
    }

    /// <summary>
    /// Builds a lean result object for recent files, including hours_ago.
    /// </summary>
    private static object BuildRecentResultObject(Everything.Interop.SearchResult r, bool includeMetadata)
    {
        var dict = new Dictionary<string, object?>
        {
            ["name"] = r.Name,
            ["path"] = r.Path
        };

        if (includeMetadata)
        {
            if (r.Size.HasValue)
                dict["size"] = r.Size.Value;
            if (r.DateModified.HasValue)
            {
                dict["date_modified"] = r.DateModified.Value.ToString("yyyy-MM-dd HH:mm:ss");
                dict["hours_ago"] = Math.Round((DateTime.Now - r.DateModified.Value).TotalHours, 1);
            }
            if (r.DateCreated.HasValue)
                dict["date_created"] = r.DateCreated.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return dict;
    }

    /// <summary>
    /// Builds the top-level response dictionary with optional folder count.
    /// </summary>
    private static Dictionary<string, object> BuildResponse(
        int totalFound,
        IReadOnlyList<Everything.Interop.SearchResult> returnedResults,
        IReadOnlyList<object> mappedResults)
    {
        var response = new Dictionary<string, object>
        {
            ["total_found"] = totalFound,
            ["returned"] = mappedResults.Count
        };

        var folderCount = returnedResults.Count(r => r.IsFolder);
        if (folderCount > 0)
            response["folders"] = folderCount;

        response["results"] = mappedResults;
        return response;
    }

    private string BuildScopedQuery(string query, string scope)
    {
        scope = scope.ToLower().Trim();

        return scope switch
        {
            "system" => query, // System-wide search (original behavior)
            "current" => $"\"{Environment.CurrentDirectory}\\\" {query}", // Current directory only
            "recursive" => $"path:\"{Environment.CurrentDirectory}\" {query}", // Current directory and subdirectories
            var custom when custom.StartsWith("path:") => BuildCustomPathQuery(custom, query),
            _ => $"path:\"{Environment.CurrentDirectory}\" {query}" // Default to recursive current
        };
    }

    private string BuildCustomPathQuery(string custom, string query)
    {
        var pathPart = custom.Substring(5).Trim(); // Remove "path:" prefix and trim whitespace
        pathPart = NormalizeToWindowsPath(pathPart);
        return $"path:\"{pathPart}\" {query}";
    }

    /// <summary>
    /// Converts Unix-style paths (e.g. /c/Users/me or c:/foo) to Windows paths (C:\Users\me).
    /// LLMs running in Git Bash/MSYS2 often produce Unix paths that Everything cannot match.
    /// </summary>
    private static string NormalizeToWindowsPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        path = path.Trim().TrimStart('/', '\\');

        // Convert /c/Users/... or c/Users/... (after trim) to C:\Users\...
        // Match a single drive letter followed by / at position 0
        if (path.Length >= 2 && char.IsLetter(path[0]) && path[1] == '/')
        {
            path = char.ToUpper(path[0]) + ":" + path.Substring(1);
        }

        // Replace remaining forward slashes with backslashes
        path = path.Replace('/', '\\');

        return path;
    }
}