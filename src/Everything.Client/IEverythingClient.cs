using Everything.Interop;

namespace Everything.Client;

public interface IEverythingClient : IDisposable
{
    bool IsEverythingRunning { get; }
    string EverythingVersion { get; }
    TargetMachine TargetMachine { get; }
    bool IsDbLoaded { get; }
    bool IsDbBusy { get; }
    bool IsAdmin { get; }

    Task<SearchResult[]> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<SearchResult[]> SearchAsync(string query, SearchFlags flags, CancellationToken cancellationToken = default);
    Task<SearchResult[]> SearchAsync(string query, SearchFlags flags, SortType sort, CancellationToken cancellationToken = default);
    Task<SearchResult[]> SearchAsync(string query, SearchFlags flags, SortType sort, uint maxResults, CancellationToken cancellationToken = default);
    Task<SearchResult[]> SearchAsync(SearchOptions options, CancellationToken cancellationToken = default);

    Task<SearchResult[]> SearchFilesAsync(string query, CancellationToken cancellationToken = default);
    Task<SearchResult[]> SearchFoldersAsync(string query, CancellationToken cancellationToken = default);
    Task<SearchResult[]> SearchByExtensionAsync(string extension, CancellationToken cancellationToken = default);
    Task<SearchResult[]> SearchByPathAsync(string path, CancellationToken cancellationToken = default);

    Task RefreshConnectionAsync(CancellationToken cancellationToken = default);
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