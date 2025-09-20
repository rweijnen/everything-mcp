using Everything.Interop;

namespace Everything.Client;

public class EverythingClientOptions
{
    public int DefaultTimeoutMs { get; set; } = 5000;
    public uint DefaultMaxResults { get; set; } = 1000;
    public SortType DefaultSort { get; set; } = SortType.NameAscending;
    public SearchFlags DefaultSearchFlags { get; set; } = SearchFlags.None;
    public Query2RequestFlags DefaultRequestFlags { get; set; } =
        Query2RequestFlags.Name | Query2RequestFlags.Path | Query2RequestFlags.Size;
    public bool EnableAutoRefresh { get; set; } = true;
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(1);
}