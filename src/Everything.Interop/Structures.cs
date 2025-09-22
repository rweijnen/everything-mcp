using System.Runtime.InteropServices;

namespace Everything.Interop;

/// <summary>
/// Represents the COPYDATASTRUCT used for WM_COPYDATA message communication.
/// This is the native Windows structure for inter-process data transfer.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CopyDataStruct
{
    /// <summary>User-defined data type identifier.</summary>
    public IntPtr dwData;
    /// <summary>Size of the data pointed to by lpData, in bytes.</summary>
    public uint cbData;
    /// <summary>Pointer to the data to be transferred.</summary>
    public IntPtr lpData;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct EverythingIpcCommandLine
{
    public uint ShowCommand;
    public fixed byte CommandLineText[1];
}

/// <summary>
/// Everything IPC QUERY1 structure for basic Unicode searches.
/// Used for fast name/path-only queries without metadata.
/// </summary>
/// <remarks>
/// QUERY1 is the fastest search protocol (~20-30ms) but only returns basic file information.
/// For metadata like size, dates, or attributes, use EverythingIpcQuery2W instead.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
public unsafe struct EverythingIpcQueryW
{
    /// <summary>Window handle to receive the reply message.</summary>
    public uint ReplyHwnd;  // DWORD - only 32bits required even on x64
    /// <summary>Message ID for the reply (usually WM_COPYDATA).</summary>
    public uint ReplyCopyDataMessage;
    /// <summary>Search flags controlling search behavior.</summary>
    public uint SearchFlags;
    /// <summary>Number of results to skip (for pagination).</summary>
    public uint Offset;
    /// <summary>Maximum number of results to return.</summary>
    public uint MaxResults;
    /// <summary>Variable-length Unicode search string.</summary>
    public fixed char SearchString[1];
}

/// <summary>
/// Everything IPC QUERY2 structure for advanced Unicode searches with metadata.
/// Used for searches that require file metadata like size, dates, and attributes.
/// </summary>
/// <remarks>
/// QUERY2 provides rich metadata but is slower than QUERY1 (~50-100ms).
/// Supports file sizes, creation/modification/access dates, and file attributes.
/// The RequestFlags field controls which metadata fields are returned.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
public unsafe struct EverythingIpcQuery2W
{
    /// <summary>Window handle to receive the reply message.</summary>
    public uint ReplyHwnd;  // DWORD - only 32bits required even on x64
    /// <summary>Message ID for the reply (usually WM_COPYDATA).</summary>
    public uint ReplyCopyDataMessage;
    /// <summary>Search flags controlling search behavior.</summary>
    public uint SearchFlags;
    /// <summary>Number of results to skip (for pagination).</summary>
    public uint Offset;
    /// <summary>Maximum number of results to return.</summary>
    public uint MaxResults;
    /// <summary>Flags specifying which metadata fields to include in results.</summary>
    public uint RequestFlags;
    /// <summary>Sort type for the results.</summary>
    public uint SortType;
    /// <summary>Variable-length Unicode search string.</summary>
    public fixed char SearchString[1];
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public unsafe struct EverythingIpcQueryA
{
    public uint ReplyHwnd;  // DWORD - only 32bits required even on x64
    public uint ReplyCopyDataMessage;
    public uint SearchFlags;
    public uint Offset;
    public uint MaxResults;
    public fixed byte SearchString[1];
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EverythingIpcItemW
{
    public uint Flags;
    public uint FilenameOffset;
    public uint PathOffset;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EverythingIpcItemA
{
    public uint Flags;
    public uint FilenameOffset;
    public uint PathOffset;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct EverythingIpcListW
{
    public uint TotalFolders;
    public uint TotalFiles;
    public uint TotalItems;
    public uint NumFolders;
    public uint NumFiles;
    public uint NumItems;
    public uint Offset;
    public EverythingIpcItemW Items;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct EverythingIpcListA
{
    public uint TotalFolders;
    public uint TotalFiles;
    public uint TotalItems;
    public uint NumFolders;
    public uint NumFiles;
    public uint NumItems;
    public uint Offset;
    public EverythingIpcItemA Items;
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
public unsafe struct EverythingIpcQuery2
{
    public uint ReplyHwnd;
    public uint ReplyCopyDataMessage;
    public uint SearchFlags;
    public uint Offset;
    public uint MaxResults;
    public uint RequestFlags;
    public uint SortType;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EverythingIpcItem2
{
    public uint Flags;
    public uint DataOffset;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct EverythingIpcList2
{
    public uint TotalItems;
    public uint NumItems;
    public uint Offset;
    public uint RequestFlags;
    public uint SortType;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct EverythingIpcRunHistory
{
    public uint RunCount;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct EverythingIpcList2W
{
    public uint TotalItems;    // totitems - number of items found
    public uint NumItems;      // numitems - the number of items available
    public uint Offset;        // index offset of the first result
    public uint RequestFlags;  // valid request types
    public uint SortType;      // sort type used
    // followed by: EVERYTHING_IPC_ITEM2 items[numitems]
    // followed by: item data
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct EverythingIpcItem2W
{
    public uint Flags;      // item flags (EVERYTHING_IPC_FOLDER|EVERYTHING_IPC_DRIVE|EVERYTHING_IPC_ROOT)
    public uint DataOffset; // offset from start of EVERYTHING_IPC_LIST2 to data content
}

/// <summary>
/// Represents a search result returned by Everything Search Engine.
/// Contains file/folder information with optional metadata depending on the query type.
/// </summary>
/// <param name="Name">The filename or folder name without path.</param>
/// <param name="Path">The directory path containing this item.</param>
/// <param name="FullPath">The complete path including filename.</param>
/// <param name="Flags">Item type flags (file, folder, drive, etc.).</param>
/// <param name="Size">File size in bytes (null for folders or if not requested).</param>
/// <param name="DateCreated">Creation date (null if not requested).</param>
/// <param name="DateModified">Last modification date (null if not requested).</param>
/// <param name="DateAccessed">Last access date (null if not requested).</param>
/// <param name="Attributes">File attributes as Windows file attribute flags (null if not requested).</param>
/// <param name="RunCount">Number of times executed (null if not requested).</param>
/// <param name="DateRun">Last execution date (null if not requested).</param>
public readonly record struct SearchResult(
    string Name,
    string Path,
    string FullPath,
    ItemFlags Flags,
    long? Size = null,
    DateTime? DateCreated = null,
    DateTime? DateModified = null,
    DateTime? DateAccessed = null,
    uint? Attributes = null,
    uint? RunCount = null,
    DateTime? DateRun = null)
{
    /// <summary>Gets a value indicating whether this item is a folder.</summary>
    public bool IsFolder => Flags.HasFlag(ItemFlags.Folder);
    /// <summary>Gets a value indicating whether this item is a drive.</summary>
    public bool IsDrive => Flags.HasFlag(ItemFlags.Drive);
    /// <summary>Gets a value indicating whether this item is a file (not a folder).</summary>
    public bool IsFile => !IsFolder;
}

/// <summary>
/// Configures search parameters for Everything queries.
/// Controls query behavior, sorting, pagination, and metadata retrieval.
/// </summary>
/// <param name="Query">The search query string using Everything syntax.</param>
/// <param name="Flags">Search behavior flags (match case, whole word, etc.).</param>
/// <param name="Sort">Sort order for results (name, size, date, etc.).</param>
/// <param name="Offset">Number of results to skip for pagination (0-based).</param>
/// <param name="MaxResults">Maximum number of results to return (use Constants.EVERYTHING_IPC_ALLRESULTS for all).</param>
/// <param name="RequestFlags">Metadata fields to include in results (QUERY2 only).</param>
/// <param name="QueryType">Force specific query protocol or auto-select based on RequestFlags.</param>
public readonly record struct SearchOptions(
    string Query,
    SearchFlags Flags = SearchFlags.None,
    SortType Sort = SortType.NameAscending,
    uint Offset = 0,
    uint MaxResults = Constants.EVERYTHING_IPC_ALLRESULTS,
    Query2RequestFlags RequestFlags = Query2RequestFlags.Name | Query2RequestFlags.Path,
    QueryType QueryType = QueryType.Auto)
{
    /// <summary>
    /// Create basic search options for QUERY1 (fast, name/path only)
    /// </summary>
    public static SearchOptions Basic(string query, SearchFlags flags = SearchFlags.None) =>
        new(query, flags, QueryType: QueryType.Query1);

    /// <summary>
    /// Create extended search options for QUERY2 with full metadata
    /// </summary>
    public static SearchOptions Extended(string query, SearchFlags flags = SearchFlags.None) =>
        new(query, flags, RequestFlags: Query2RequestFlags.All, QueryType: QueryType.Query2);

    /// <summary>
    /// Create search options with specific metadata fields
    /// </summary>
    public static SearchOptions WithMetadata(string query, Query2RequestFlags requestFlags, SearchFlags flags = SearchFlags.None) =>
        new(query, flags, RequestFlags: requestFlags | Query2RequestFlags.Name | Query2RequestFlags.Path, QueryType: QueryType.Query2);

    /// <summary>
    /// Determine if this search requires QUERY2 based on request flags
    /// </summary>
    public bool RequiresQuery2 => QueryType switch
    {
        QueryType.Query1 => false,
        QueryType.Query2 => true,
        QueryType.Auto => RequestFlags.HasFlag(Query2RequestFlags.Size) ||
                         RequestFlags.HasFlag(Query2RequestFlags.DateCreated) ||
                         RequestFlags.HasFlag(Query2RequestFlags.DateModified) ||
                         RequestFlags.HasFlag(Query2RequestFlags.DateAccessed) ||
                         RequestFlags.HasFlag(Query2RequestFlags.Attributes) ||
                         RequestFlags.HasFlag(Query2RequestFlags.RunCount) ||
                         RequestFlags.HasFlag(Query2RequestFlags.DateRun) ||
                         RequestFlags.HasFlag(Query2RequestFlags.FullPathAndName) ||
                         RequestFlags.HasFlag(Query2RequestFlags.Extension),
        _ => false
    };
};