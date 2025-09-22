using Everything.Interop;

namespace Everything.Client;

/// <summary>
/// Defines the contract for a high-level Everything Search Engine client.
/// Provides async search operations with automatic protocol selection and thread safety.
/// </summary>
/// <remarks>
/// Implementations should handle:
/// - Thread-safe communication with Everything Search Engine
/// - Automatic selection between QUERY1 and QUERY2 protocols
/// - Connection state management and error handling
/// - Proper resource disposal and cleanup
/// </remarks>
public interface IEverythingClient : IDisposable
{
    /// <summary>Gets a value indicating whether Everything Search Engine is running and accessible.</summary>
    bool IsEverythingRunning { get; }
    /// <summary>Gets the version string of the Everything Search Engine.</summary>
    string EverythingVersion { get; }
    /// <summary>Gets the target machine architecture (x86, x64, etc.).</summary>
    TargetMachine TargetMachine { get; }
    /// <summary>Gets a value indicating whether the Everything database is loaded.</summary>
    bool IsDbLoaded { get; }
    /// <summary>Gets a value indicating whether the Everything database is currently busy (indexing, etc.).</summary>
    bool IsDbBusy { get; }
    /// <summary>Gets a value indicating whether Everything is running with administrator privileges.</summary>
    bool IsAdmin { get; }

    /// <summary>Searches using the specified query with default options.</summary>
    Task<SearchResult[]> SearchAsync(string query, CancellationToken cancellationToken = default);
    /// <summary>Searches using the specified query and search flags.</summary>
    Task<SearchResult[]> SearchAsync(string query, SearchFlags flags, CancellationToken cancellationToken = default);
    /// <summary>Searches using the specified query, flags, and sort order.</summary>
    Task<SearchResult[]> SearchAsync(string query, SearchFlags flags, SortType sort, CancellationToken cancellationToken = default);
    /// <summary>Searches using the specified query, flags, sort order, and result limit.</summary>
    Task<SearchResult[]> SearchAsync(string query, SearchFlags flags, SortType sort, uint maxResults, CancellationToken cancellationToken = default);
    /// <summary>Searches using comprehensive search options (primary search method).</summary>
    Task<SearchResult[]> SearchAsync(SearchOptions options, CancellationToken cancellationToken = default);

    // Efficiency-focused methods
    /// <summary>Performs a fast basic search with minimal metadata (QUERY1 protocol).</summary>
    Task<SearchResult[]> SearchBasicAsync(string query, SearchFlags flags = SearchFlags.None, CancellationToken cancellationToken = default);
    /// <summary>Performs a comprehensive search with full metadata (QUERY2 protocol).</summary>
    Task<SearchResult[]> SearchWithMetadataAsync(string query, Query2RequestFlags requestFlags = Query2RequestFlags.All, SearchFlags flags = SearchFlags.None, CancellationToken cancellationToken = default);

    /// <summary>Searches for files only (excludes folders).</summary>
    Task<SearchResult[]> SearchFilesAsync(string query, CancellationToken cancellationToken = default);
    /// <summary>Searches for folders only (excludes files).</summary>
    Task<SearchResult[]> SearchFoldersAsync(string query, CancellationToken cancellationToken = default);
    /// <summary>Searches for files with the specified extension.</summary>
    Task<SearchResult[]> SearchByExtensionAsync(string extension, CancellationToken cancellationToken = default);
    /// <summary>Searches within the specified path.</summary>
    Task<SearchResult[]> SearchByPathAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Refreshes the connection to Everything Search Engine.</summary>
    Task RefreshConnectionAsync(CancellationToken cancellationToken = default);
    /// <summary>Requests Everything to rebuild its file database.</summary>
    Task RebuildDatabaseAsync(CancellationToken cancellationToken = default);
    Task SaveDatabaseAsync(CancellationToken cancellationToken = default);

    event EventHandler<EverythingStatusChangedEventArgs>? StatusChanged;
}

public class EverythingStatusChangedEventArgs : EventArgs
{
    public bool IsRunning { get; init; }
    public bool IsDbLoaded { get; init; }
    public bool IsDbBusy { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}