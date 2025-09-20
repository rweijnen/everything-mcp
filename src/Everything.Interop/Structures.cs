using System.Runtime.InteropServices;

namespace Everything.Interop;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
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
    public uint ReplyHwnd;
    public uint ReplyCopyDataMessage;
    public uint SearchFlags;
    public uint Offset;
    public uint MaxResults;
    public fixed char SearchString[1];
}

[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public unsafe struct EverythingIpcQueryA
{
    public uint ReplyHwnd;
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
    Query2RequestFlags RequestFlags = Query2RequestFlags.Name | Query2RequestFlags.Path);