using System.Runtime.InteropServices;

namespace Everything.Interop;

[StructLayout(LayoutKind.Sequential)]
public struct CopyDataStruct
{
    public IntPtr dwData;
    public uint cbData;
    public IntPtr lpData;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct EverythingIpcCommandLine
{
    public uint ShowCommand;
    public fixed byte CommandLineText[1];
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
public unsafe struct EverythingIpcQueryW
{
    public uint ReplyHwnd;  // DWORD - only 32bits required even on x64
    public uint ReplyCopyDataMessage;
    public uint SearchFlags;
    public uint Offset;
    public uint MaxResults;
    public fixed char SearchString[1];
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
public unsafe struct EverythingIpcQuery2W
{
    public uint ReplyHwnd;  // DWORD - only 32bits required even on x64
    public uint ReplyCopyDataMessage;
    public uint SearchFlags;
    public uint Offset;
    public uint MaxResults;
    public uint RequestFlags;
    public uint SortType;
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
    public bool IsFolder => Flags.HasFlag(ItemFlags.Folder);
    public bool IsDrive => Flags.HasFlag(ItemFlags.Drive);
    public bool IsFile => !IsFolder;
}

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