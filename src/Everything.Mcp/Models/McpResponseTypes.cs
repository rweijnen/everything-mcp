using System.Text.Json.Serialization;

namespace Everything.Mcp.Models;

/// <summary>
/// JSON serialization context for MCP response types
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(FileResult))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(ExecutableResponse))]
[JsonSerializable(typeof(ExecutableResult))]
[JsonSerializable(typeof(ProjectSearchResponse))]
[JsonSerializable(typeof(ProjectFileResult))]
[JsonSerializable(typeof(SourceFileResponse))]
[JsonSerializable(typeof(SourceFileResult))]
[JsonSerializable(typeof(RecentFilesResponse))]
[JsonSerializable(typeof(RecentFileResult))]
[JsonSerializable(typeof(ConfigFilesResponse))]
[JsonSerializable(typeof(ConfigFileResult))]
[JsonSerializable(typeof(List<FileResult>))]
[JsonSerializable(typeof(List<ExecutableResult>))]
[JsonSerializable(typeof(List<ProjectFileResult>))]
[JsonSerializable(typeof(List<SourceFileResult>))]
[JsonSerializable(typeof(List<RecentFileResult>))]
[JsonSerializable(typeof(List<ConfigFileResult>))]
public partial class McpResponseJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Represents a file or folder result from Everything search
/// </summary>
public record FileResult
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("is_folder")]
    public required bool IsFolder { get; init; }

    [JsonPropertyName("size")]
    public long? Size { get; init; }

    [JsonPropertyName("date_modified")]
    public string? DateModified { get; init; }

    [JsonPropertyName("date_created")]
    public string? DateCreated { get; init; }

    [JsonPropertyName("date_accessed")]
    public string? DateAccessed { get; init; }
}

/// <summary>
/// Response for file search operations
/// </summary>
public record SearchResponse
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("total_found")]
    public required int TotalFound { get; init; }

    [JsonPropertyName("returned")]
    public required int Returned { get; init; }

    [JsonPropertyName("include_metadata")]
    public required bool IncludeMetadata { get; init; }

    [JsonPropertyName("results")]
    public required List<FileResult> Results { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// Response for executable search operations
/// </summary>
public record ExecutableResponse
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("exact_match")]
    public required bool ExactMatch { get; init; }

    [JsonPropertyName("total_found")]
    public required int TotalFound { get; init; }

    [JsonPropertyName("returned")]
    public required int Returned { get; init; }

    [JsonPropertyName("executables")]
    public required List<ExecutableResult> Executables { get; init; }
}

/// <summary>
/// Represents an executable file result
/// </summary>
public record ExecutableResult
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("exists")]
    public required bool Exists { get; init; }
}

/// <summary>
/// Response for project search operations
/// </summary>
public record ProjectSearchResponse
{
    [JsonPropertyName("project_path")]
    public required string ProjectPath { get; init; }

    [JsonPropertyName("pattern")]
    public required string Pattern { get; init; }

    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("total_found")]
    public required int TotalFound { get; init; }

    [JsonPropertyName("returned")]
    public required int Returned { get; init; }

    [JsonPropertyName("include_metadata")]
    public required bool IncludeMetadata { get; init; }

    [JsonPropertyName("results")]
    public required List<ProjectFileResult> Results { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// Represents a file result with relative path for project searches
/// </summary>
public record ProjectFileResult
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("relative_path")]
    public required string RelativePath { get; init; }

    [JsonPropertyName("is_folder")]
    public required bool IsFolder { get; init; }

    [JsonPropertyName("size")]
    public long? Size { get; init; }

    [JsonPropertyName("date_modified")]
    public string? DateModified { get; init; }

    [JsonPropertyName("date_created")]
    public string? DateCreated { get; init; }

    [JsonPropertyName("date_accessed")]
    public string? DateAccessed { get; init; }
}

/// <summary>
/// Response for source file search operations
/// </summary>
public record SourceFileResponse
{
    [JsonPropertyName("filename")]
    public required string Filename { get; init; }

    [JsonPropertyName("extensions_searched")]
    public required string[] ExtensionsSearched { get; init; }

    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("total_found")]
    public required int TotalFound { get; init; }

    [JsonPropertyName("returned")]
    public required int Returned { get; init; }

    [JsonPropertyName("include_metadata")]
    public required bool IncludeMetadata { get; init; }

    [JsonPropertyName("results")]
    public required List<SourceFileResult> Results { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// Represents a source file result with extension information
/// </summary>
public record SourceFileResult
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("extension")]
    public required string Extension { get; init; }

    [JsonPropertyName("is_folder")]
    public required bool IsFolder { get; init; }

    [JsonPropertyName("size")]
    public long? Size { get; init; }

    [JsonPropertyName("date_modified")]
    public string? DateModified { get; init; }

    [JsonPropertyName("date_created")]
    public string? DateCreated { get; init; }

    [JsonPropertyName("date_accessed")]
    public string? DateAccessed { get; init; }
}

/// <summary>
/// Response for recent files search operations
/// </summary>
public record RecentFilesResponse
{
    [JsonPropertyName("hours_back")]
    public required int HoursBack { get; init; }

    [JsonPropertyName("cutoff_date")]
    public required string CutoffDate { get; init; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; init; }

    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("total_found")]
    public required int TotalFound { get; init; }

    [JsonPropertyName("returned")]
    public required int Returned { get; init; }

    [JsonPropertyName("include_metadata")]
    public required bool IncludeMetadata { get; init; }

    [JsonPropertyName("results")]
    public required List<RecentFileResult> Results { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// Represents a recent file result with time information
/// </summary>
public record RecentFileResult
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("is_folder")]
    public required bool IsFolder { get; init; }

    [JsonPropertyName("size")]
    public long? Size { get; init; }

    [JsonPropertyName("date_modified")]
    public string? DateModified { get; init; }

    [JsonPropertyName("date_created")]
    public string? DateCreated { get; init; }

    [JsonPropertyName("date_accessed")]
    public string? DateAccessed { get; init; }

    [JsonPropertyName("hours_ago")]
    public double? HoursAgo { get; init; }
}

/// <summary>
/// Response for configuration files search operations
/// </summary>
public record ConfigFilesResponse
{
    [JsonPropertyName("project_path")]
    public string? ProjectPath { get; init; }

    [JsonPropertyName("config_extensions")]
    public required string[] ConfigExtensions { get; init; }

    [JsonPropertyName("config_names")]
    public required string[] ConfigNames { get; init; }

    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("total_found")]
    public required int TotalFound { get; init; }

    [JsonPropertyName("returned")]
    public required int Returned { get; init; }

    [JsonPropertyName("include_metadata")]
    public required bool IncludeMetadata { get; init; }

    [JsonPropertyName("results")]
    public required List<ConfigFileResult> Results { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

/// <summary>
/// Represents a configuration file result with type information
/// </summary>
public record ConfigFileResult
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("relative_path")]
    public string? RelativePath { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("is_folder")]
    public required bool IsFolder { get; init; }

    [JsonPropertyName("size")]
    public long? Size { get; init; }

    [JsonPropertyName("date_modified")]
    public string? DateModified { get; init; }

    [JsonPropertyName("date_created")]
    public string? DateCreated { get; init; }

    [JsonPropertyName("date_accessed")]
    public string? DateAccessed { get; init; }
}