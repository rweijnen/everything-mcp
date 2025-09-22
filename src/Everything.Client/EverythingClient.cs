using Everything.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace Everything.Client;

/// <summary>
/// High-level managed client for Everything Search Engine.
/// Provides async/await API with queue-based threading and automatic connection management.
/// </summary>
/// <remarks>
/// This client uses a dedicated Windows message window thread to handle IPC communication
/// with Everything.exe. All search operations are queued and processed sequentially
/// to ensure thread safety and proper Windows message handling.
///
/// Features:
/// - Automatic protocol selection (QUERY1 vs QUERY2)
/// - Async/await API with cancellation support
/// - Thread-safe operation via message queue
/// - Optional auto-refresh of Everything connection
/// - Comprehensive logging for debugging
/// </remarks>
public class EverythingClient : IEverythingClient
{
    private readonly EverythingIpc _ipc;
    private readonly EverythingClientOptions _options;
    private readonly ILogger<EverythingClient> _logger;
    private readonly Timer? _refreshTimer;
    private readonly MessageWindowThread _messageWindowThread;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the EverythingClient.
    /// </summary>
    /// <param name="options">Configuration options for the client behavior.</param>
    /// <param name="logger">Logger for diagnostic information and debugging.</param>
    public EverythingClient(IOptions<EverythingClientOptions> options, ILogger<EverythingClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _ipc = new EverythingIpc();
        _logger.LogInformation("Creating dedicated message window thread for Everything IPC");
        _messageWindowThread = new MessageWindowThread(_logger, _ipc.EverythingWindowHandle);

        if (_options.EnableAutoRefresh)
        {
            _refreshTimer = new Timer(OnRefreshTimer, null, _options.RefreshInterval, _options.RefreshInterval);
        }

        _logger.LogInformation("Everything client initialized");
    }

    /// <summary>
    /// Gets a value indicating whether Everything Search Engine is currently running and accessible.
    /// </summary>
    /// <value>True if Everything is running and responding; otherwise, false.</value>
    public bool IsEverythingRunning
    {
        get
        {
            try
            {
                return _ipc.IsEverythingRunning;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check if Everything is running");
                return false;
            }
        }
    }

    /// <summary>
    /// Gets the version string of the Everything Search Engine.
    /// </summary>
    /// <value>Version string in format "Major.Minor.Revision" (e.g., "1.4.1").</value>
    /// <exception cref="InvalidOperationException">Thrown when Everything is not running.</exception>
    public string EverythingVersion
    {
        get
        {
            try
            {
                return _ipc.GetVersionString();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get Everything version");
                return "Unknown";
            }
        }
    }

    public TargetMachine TargetMachine
    {
        get
        {
            try
            {
                return _ipc.GetTargetMachine();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get target machine");
                return TargetMachine.X64;
            }
        }
    }

    public bool IsDbLoaded
    {
        get
        {
            try
            {
                return _ipc.IsDbLoaded();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check if database is loaded");
                return false;
            }
        }
    }

    public bool IsDbBusy
    {
        get
        {
            try
            {
                return _ipc.IsDbBusy();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check if database is busy");
                return false;
            }
        }
    }

    public bool IsAdmin
    {
        get
        {
            try
            {
                return _ipc.IsAdmin();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check admin status");
                return false;
            }
        }
    }

    public event EventHandler<EverythingStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Searches for files and folders using the specified query string.
    /// Uses default search options configured for this client instance.
    /// </summary>
    /// <param name="query">The search query using Everything syntax (e.g., "*.txt", "folder: test").</param>
    /// <param name="cancellationToken">Token to cancel the search operation.</param>
    /// <returns>An array of search results matching the query.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Everything is not running.</exception>
    /// <exception cref="ArgumentException">Thrown when the query is invalid or too long.</exception>
    public Task<SearchResult[]> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var options = new SearchOptions(
            Query: query,
            Flags: _options.DefaultSearchFlags,
            Sort: _options.DefaultSort,
            MaxResults: _options.DefaultMaxResults,
            RequestFlags: _options.DefaultRequestFlags);

        return SearchAsync(options, cancellationToken);
    }

    public Task<SearchResult[]> SearchAsync(string query, SearchFlags flags, CancellationToken cancellationToken = default)
    {
        var options = new SearchOptions(
            Query: query,
            Flags: flags,
            Sort: _options.DefaultSort,
            MaxResults: _options.DefaultMaxResults,
            RequestFlags: _options.DefaultRequestFlags);

        return SearchAsync(options, cancellationToken);
    }

    public Task<SearchResult[]> SearchAsync(string query, SearchFlags flags, SortType sort, CancellationToken cancellationToken = default)
    {
        var options = new SearchOptions(
            Query: query,
            Flags: flags,
            Sort: sort,
            MaxResults: _options.DefaultMaxResults,
            RequestFlags: _options.DefaultRequestFlags);

        return SearchAsync(options, cancellationToken);
    }

    public Task<SearchResult[]> SearchAsync(string query, SearchFlags flags, SortType sort, uint maxResults, CancellationToken cancellationToken = default)
    {
        var options = new SearchOptions(
            Query: query,
            Flags: flags,
            Sort: sort,
            MaxResults: maxResults,
            RequestFlags: _options.DefaultRequestFlags);

        return SearchAsync(options, cancellationToken);
    }

    /// <summary>
    /// Searches for files and folders using detailed search options.
    /// This is the main search method that all other overloads ultimately call.
    /// </summary>
    /// <param name="options">Complete search configuration including query, flags, sorting, and metadata options.</param>
    /// <param name="cancellationToken">Token to cancel the search operation.</param>
    /// <returns>An array of search results with metadata as specified in options.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Everything is not running.</exception>
    /// <exception cref="ArgumentException">Thrown when the query is invalid or too long.</exception>
    /// <exception cref="TimeoutException">Thrown when the search times out.</exception>
    /// <remarks>
    /// This method automatically selects the optimal query protocol:
    /// - QUERY1: For basic name/path searches (faster)
    /// - QUERY2: When metadata like size, dates, or attributes are requested
    ///
    /// All search operations are queued and processed on a dedicated message window thread
    /// to ensure proper Windows IPC handling and thread safety.
    /// </remarks>
    public async Task<SearchResult[]> SearchAsync(SearchOptions options, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching for: {Query} with flags: {Flags}", options.Query, options.Flags);

        try
        {
            _logger.LogDebug("Delegating search to dedicated message window thread");
            var results = await _messageWindowThread.QueryAsync(options, _options.DefaultTimeoutMs, cancellationToken);
            _logger.LogDebug("Search completed with {Count} results", results.Length);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", options.Query);
            throw;
        }
    }

    /// <summary>
    /// Performs a fast basic search returning only name and path information.
    /// Forces use of QUERY1 protocol for maximum performance.
    /// </summary>
    /// <param name="query">The search query using Everything syntax.</param>
    /// <param name="flags">Search behavior flags (case sensitivity, regex, etc.).</param>
    /// <param name="cancellationToken">Token to cancel the search operation.</param>
    /// <returns>An array of basic search results without metadata.</returns>
    /// <remarks>
    /// This method is optimized for speed (~20-30ms) and only returns basic file information.
    /// Use SearchWithMetadataAsync() when you need file sizes, dates, or attributes.
    /// </remarks>
    public Task<SearchResult[]> SearchBasicAsync(string query, SearchFlags flags = SearchFlags.None, CancellationToken cancellationToken = default)
    {
        var options = SearchOptions.Basic(query, flags);
        return SearchAsync(options, cancellationToken);
    }

    /// <summary>
    /// Performs a comprehensive search with full metadata using QUERY2 protocol.
    /// Returns detailed file information including sizes, dates, and attributes.
    /// </summary>
    /// <param name="query">The search query using Everything syntax.</param>
    /// <param name="requestFlags">Specific metadata fields to include in results.</param>
    /// <param name="flags">Search behavior flags (case sensitivity, regex, etc.).</param>
    /// <param name="cancellationToken">Token to cancel the search operation.</param>
    /// <returns>An array of search results with comprehensive metadata.</returns>
    /// <remarks>
    /// This method is slower than SearchBasicAsync (~50-100ms) but provides rich metadata.
    /// Use Query2RequestFlags to control which metadata fields are retrieved to optimize performance.
    /// </remarks>
    public Task<SearchResult[]> SearchWithMetadataAsync(string query, Query2RequestFlags requestFlags = Query2RequestFlags.All, SearchFlags flags = SearchFlags.None, CancellationToken cancellationToken = default)
    {
        var options = SearchOptions.WithMetadata(query, requestFlags, flags);
        return SearchAsync(options, cancellationToken);
    }

    public Task<SearchResult[]> SearchFilesAsync(string query, CancellationToken cancellationToken = default)
    {
        var modifiedQuery = string.IsNullOrEmpty(query) ? "file:" : $"file: {query}";
        return SearchAsync(modifiedQuery, cancellationToken);
    }

    public Task<SearchResult[]> SearchFoldersAsync(string query, CancellationToken cancellationToken = default)
    {
        var modifiedQuery = string.IsNullOrEmpty(query) ? "folder:" : $"folder: {query}";
        return SearchAsync(modifiedQuery, cancellationToken);
    }

    public Task<SearchResult[]> SearchByExtensionAsync(string extension, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(extension))
            throw new ArgumentException("Extension cannot be null or empty", nameof(extension));

        var cleanExtension = extension.StartsWith('.') ? extension[1..] : extension;
        var query = $"ext:{cleanExtension}";
        return SearchAsync(query, cancellationToken);
    }

    public Task<SearchResult[]> SearchByPathAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        var query = $"path:\"{path}\"";
        return SearchAsync(query, SearchFlags.MatchPath, cancellationToken);
    }

    public Task RefreshConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            _logger.LogDebug("Refreshing connection to Everything");
            _ipc.RefreshEverythingWindow();
            NotifyStatusChanged();
        }, cancellationToken);
    }

    public Task RebuildDatabaseAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            _logger.LogInformation("Rebuilding Everything database");
            _ipc.RebuildDb();
        }, cancellationToken);
    }

    public Task SaveDatabaseAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            _logger.LogDebug("Saving Everything database");
            _ipc.SaveDb();
        }, cancellationToken);
    }

    private void OnRefreshTimer(object? state)
    {
        try
        {
            _ipc.RefreshEverythingWindow();
            NotifyStatusChanged();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh Everything connection");
        }
    }

    private void NotifyStatusChanged()
    {
        try
        {
            var args = new EverythingStatusChangedEventArgs
            {
                IsRunning = IsEverythingRunning,
                IsDbLoaded = IsDbLoaded,
                IsDbBusy = IsDbBusy
            };

            StatusChanged?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify status change");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _refreshTimer?.Dispose();
                _logger.LogInformation("Disposing Everything client and message window thread");
        _messageWindowThread?.Dispose();
                _ipc?.Dispose();
                _logger.LogInformation("Everything client disposed");
            }
            _disposed = true;
        }
    }
}