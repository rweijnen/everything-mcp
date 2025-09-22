using Everything.Client;
using Everything.Mcp.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;

namespace Everything.Mcp;

[McpServerToolType]
public class EverythingMcpTools
{
    private readonly IEverythingClient _everythingClient;
    private readonly ILogger<EverythingMcpTools> _logger;

    public EverythingMcpTools(IEverythingClient everythingClient, ILogger<EverythingMcpTools> logger)
    {
        _everythingClient = everythingClient;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Search for files and folders using Everything search. Supports wildcards, regex, boolean operators (AND/OR/NOT), size filters, and more.")]
    public async Task<object> search_files(
        [Description("Search query with Everything syntax: wildcards (*.cs), regex (regex:pattern), boolean (!exclude, file1|file2), size (size:>1MB), etc.")] string query,
        [Description("Search scope: 'current' (default), 'recursive', 'path:/some/folder', or 'system' for system-wide")] string scope = "current",
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

            var limitedResults = results.Take(max_results).ToList();

            return new SearchResponse
            {
                Query = query,
                Scope = scope,
                TotalFound = results.Length,
                Returned = limitedResults.Count,
                IncludeMetadata = include_metadata,
                Results = limitedResults.Select(r => new FileResult
                {
                    Name = r.Name,
                    Path = r.Path,
                    IsFolder = r.IsFolder,
                    Size = include_metadata ? r.Size : null,
                    DateModified = include_metadata ? r.DateModified?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateCreated = include_metadata ? r.DateCreated?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateAccessed = include_metadata ? r.DateAccessed?.ToString("yyyy-MM-dd HH:mm:ss") : null
                }).ToList()
            };
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while searching files with query: {Query}", query);
            return new SearchResponse
            {
                Query = query,
                Scope = scope,
                TotalFound = 0,
                Returned = 0,
                IncludeMetadata = include_metadata,
                Error = "Service is shutting down, please try again",
                Results = new List<FileResult>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching files with query: {Query}, scope: {Scope}", query, scope);
            throw;
        }
    }

    [McpServerTool]
    [Description("Search for files in a specific project folder recursively.")]
    public async Task<object> search_in_project(
        [Description("Project folder path")] string project_path,
        [Description("Search pattern (e.g., *.cs, test*.txt)")] string pattern,
        [Description("Include metadata like size, dates (default: false)")] bool include_metadata = false,
        [Description("Maximum number of results (default: 100)")] int max_results = 100)
    {
        try
        {
            var normalizedPath = Path.GetFullPath(project_path).TrimEnd('\\', '/');
            var query = $"{normalizedPath}\\{pattern}";

            var results = include_metadata
                ? await _everythingClient.SearchWithMetadataAsync(query)
                : await _everythingClient.SearchBasicAsync(query);

            var limitedResults = results.Take(max_results).ToList();

            return new ProjectSearchResponse
            {
                ProjectPath = project_path,
                Pattern = pattern,
                Query = query,
                TotalFound = results.Length,
                Returned = limitedResults.Count,
                IncludeMetadata = include_metadata,
                Results = limitedResults.Select(r => new ProjectFileResult
                {
                    Name = r.Name,
                    Path = r.Path,
                    RelativePath = Path.GetRelativePath(normalizedPath, r.Path),
                    IsFolder = r.IsFolder,
                    Size = include_metadata ? r.Size : null,
                    DateModified = include_metadata ? r.DateModified?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateCreated = include_metadata ? r.DateCreated?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateAccessed = include_metadata ? r.DateAccessed?.ToString("yyyy-MM-dd HH:mm:ss") : null
                }).ToList()
            };
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while searching in project {ProjectPath} with pattern: {Pattern}", project_path, pattern);
            return new ProjectSearchResponse
            {
                ProjectPath = project_path,
                Pattern = pattern,
                Query = $"{project_path}\\{pattern}",
                TotalFound = 0,
                Returned = 0,
                IncludeMetadata = include_metadata,
                Error = "Service is shutting down, please try again",
                Results = new List<ProjectFileResult>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching in project {ProjectPath} with pattern: {Pattern}", project_path, pattern);
            throw;
        }
    }

    [McpServerTool]
    [Description("Find executable files (.exe, .bat, .cmd, .ps1) by name.")]
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

            var limitedResults = results.Take(max_results).ToList();
            _logger.LogDebug("Limited to {Count} results", limitedResults.Count);

            return new ExecutableResponse
            {
                Query = name,
                ExactMatch = shouldBeExact,
                TotalFound = results.Length,
                Returned = limitedResults.Count,
                Executables = limitedResults.Select(r => new ExecutableResult
                {
                    Name = r.Name,
                    Path = r.Path,
                    Exists = true // All results from Everything exist
                }).ToList()
            };
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while finding executable: {Name}", name);
            return new ExecutableResponse
            {
                Query = name,
                ExactMatch = exact_match,
                TotalFound = 0,
                Returned = 0,
                Executables = new List<ExecutableResult>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding executable: {Name}", name);
            throw;
        }
    }

    [McpServerTool]
    [Description("Find source code files with common programming extensions.")]
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

            var limitedResults = results.Take(max_results).ToList();

            return new SourceFileResponse
            {
                Filename = filename,
                ExtensionsSearched = allExtensions,
                Query = query,
                TotalFound = results.Length,
                Returned = limitedResults.Count,
                IncludeMetadata = include_metadata,
                Results = limitedResults.Select(r => new SourceFileResult
                {
                    Name = r.Name,
                    Path = r.Path,
                    Extension = Path.GetExtension(r.Name),
                    IsFolder = r.IsFolder,
                    Size = include_metadata ? r.Size : null,
                    DateModified = include_metadata ? r.DateModified?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateCreated = include_metadata ? r.DateCreated?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateAccessed = include_metadata ? r.DateAccessed?.ToString("yyyy-MM-dd HH:mm:ss") : null
                }).ToList()
            };
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while finding source files: {Filename}", filename);
            return new SourceFileResponse
            {
                Filename = filename,
                ExtensionsSearched = new string[0],
                Query = "",
                TotalFound = 0,
                Returned = 0,
                IncludeMetadata = include_metadata,
                Error = "Service is shutting down, please try again",
                Results = new List<SourceFileResult>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding source files: {Filename}", filename);
            throw;
        }
    }

    [McpServerTool]
    [Description("Search for recently modified files within a time period.")]
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

            return new RecentFilesResponse
            {
                HoursBack = hours,
                CutoffDate = cutoffDate.ToString("yyyy-MM-dd HH:mm:ss"),
                Pattern = pattern,
                Query = query,
                TotalFound = results.Length,
                Returned = sortedResults.Count,
                IncludeMetadata = include_metadata,
                Results = sortedResults.Select(r => new RecentFileResult
                {
                    Name = r.Name,
                    Path = r.Path,
                    IsFolder = r.IsFolder,
                    Size = include_metadata ? r.Size : null,
                    DateModified = include_metadata ? r.DateModified?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateCreated = include_metadata ? r.DateCreated?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateAccessed = include_metadata ? r.DateAccessed?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    HoursAgo = r.DateModified.HasValue ? (DateTime.Now - r.DateModified.Value).TotalHours : null
                }).ToList()
            };
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while searching recent files");
            return new RecentFilesResponse
            {
                HoursBack = hours,
                CutoffDate = DateTime.Now.AddHours(-hours).ToString("yyyy-MM-dd HH:mm:ss"),
                Pattern = pattern,
                Query = "",
                TotalFound = 0,
                Returned = 0,
                IncludeMetadata = include_metadata,
                Error = "Service is shutting down, please try again",
                Results = new List<RecentFileResult>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching recent files");
            throw;
        }
    }

    [McpServerTool]
    [Description("Find configuration files (json, xml, yaml, ini, config) in a project.")]
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
                var normalizedPath = Path.GetFullPath(project_path).TrimEnd('\\', '/');
                var pathQueries = allQueries.Select(q => $"{normalizedPath}\\{q}");
                query = string.Join("|", pathQueries);
            }
            else
            {
                query = string.Join("|", allQueries);
            }

            var results = include_metadata
                ? await _everythingClient.SearchWithMetadataAsync(query)
                : await _everythingClient.SearchBasicAsync(query);

            var limitedResults = results.Take(max_results).ToList();

            return new ConfigFilesResponse
            {
                ProjectPath = project_path,
                ConfigExtensions = configExtensions,
                ConfigNames = configNames,
                Query = query,
                TotalFound = results.Length,
                Returned = limitedResults.Count,
                IncludeMetadata = include_metadata,
                Results = limitedResults.Select(r => new ConfigFileResult
                {
                    Name = r.Name,
                    Path = r.Path,
                    RelativePath = !string.IsNullOrEmpty(project_path) ? Path.GetRelativePath(project_path, r.Path) : null,
                    Type = Path.GetExtension(r.Name).ToLowerInvariant(),
                    IsFolder = r.IsFolder,
                    Size = include_metadata ? r.Size : null,
                    DateModified = include_metadata ? r.DateModified?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateCreated = include_metadata ? r.DateCreated?.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    DateAccessed = include_metadata ? r.DateAccessed?.ToString("yyyy-MM-dd HH:mm:ss") : null
                }).ToList()
            };
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Everything client disposed while finding config files in: {ProjectPath}", project_path);
            return new ConfigFilesResponse
            {
                ProjectPath = project_path,
                ConfigExtensions = new string[0],
                ConfigNames = new string[0],
                Query = "",
                TotalFound = 0,
                Returned = 0,
                IncludeMetadata = include_metadata,
                Error = "Service is shutting down, please try again",
                Results = new List<ConfigFileResult>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding config files in: {ProjectPath}", project_path);
            throw;
        }
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
        // Remove only leading slashes
        pathPart = pathPart.TrimStart('/', '\\');
        return $"path:\"{pathPart}\" {query}";
    }
}