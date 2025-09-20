using Everything.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace Everything.Client;

public class EverythingClient : IEverythingClient
{
    private readonly EverythingIpc _ipc;
    private readonly EverythingClientOptions _options;
    private readonly ILogger<EverythingClient> _logger;
    private readonly Timer? _refreshTimer;
    private readonly Win32MessageWindow _messageWindow;
    private bool _disposed = false;

    public EverythingClient(IOptions<EverythingClientOptions> options, ILogger<EverythingClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _ipc = new EverythingIpc();
        _messageWindow = new Win32MessageWindow();

        if (_options.EnableAutoRefresh)
        {
            _refreshTimer = new Timer(OnRefreshTimer, null, _options.RefreshInterval, _options.RefreshInterval);
        }

        _logger.LogInformation("Everything client initialized");
    }

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

    public async Task<SearchResult[]> SearchAsync(SearchOptions options, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching for: {Query} with flags: {Flags}", options.Query, options.Flags);

        try
        {
            var results = await _messageWindow.QueryAsync(options, _options.DefaultTimeoutMs, cancellationToken);
            _logger.LogDebug("Search completed with {Count} results", results.Length);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", options.Query);
            throw;
        }
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
                _messageWindow?.Dispose();
                _ipc?.Dispose();
                _logger.LogInformation("Everything client disposed");
            }
            _disposed = true;
        }
    }
}