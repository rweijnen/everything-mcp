namespace Everything.Interop;

public static class Constants
{
    public const uint WM_USER = 0x0400;
    public const uint EVERYTHING_WM_IPC = WM_USER;

    public const uint WM_COPYDATA = 0x004A;

    public const string EVERYTHING_IPC_WNDCLASS = "EVERYTHING_TASKBAR_NOTIFICATION";
    public const string EVERYTHING_IPC_SEARCH_CLIENT_WNDCLASS = "EVERYTHING";

    public const string EVERYTHING_IPC_CREATED = "EVERYTHING_IPC_CREATED";

    public const uint EVERYTHING_IPC_ALLRESULTS = 0xFFFFFFFF;
}

public static class EverythingIpcCommands
{
    public const uint GET_MAJOR_VERSION = 0;
    public const uint GET_MINOR_VERSION = 1;
    public const uint GET_REVISION = 2;
    public const uint GET_BUILD_NUMBER = 3;
    public const uint EXIT = 4;
    public const uint GET_TARGET_MACHINE = 5;

    public const uint IS_DB_LOADED = 401;
    public const uint IS_DB_BUSY = 402;
    public const uint IS_ADMIN = 403;
    public const uint IS_APPDATA = 404;
    public const uint REBUILD_DB = 405;
    public const uint UPDATE_ALL_FOLDER_INDEXES = 406;
    public const uint SAVE_DB = 407;
    public const uint SAVE_RUN_HISTORY = 408;
    public const uint DELETE_RUN_HISTORY = 409;
    public const uint IS_FAST_SORT = 410;
    public const uint IS_FILE_INFO_INDEXED = 411;
    public const uint QUEUE_REBUILD_DB = 412;

    public const uint IS_NTFS_DRIVE_INDEXED = 400;
}

public static class CopyDataMessages
{
    public const uint COMMAND_LINE_UTF8 = 0;
    public const uint COPYDATAQUERYA = 1;
    public const uint COPYDATAQUERYW = 2;
    public const uint COPYDATA_QUERY2A = 17;
    public const uint COPYDATA_QUERY2W = 18;
    public const uint GET_RUN_COUNTA = 19;
    public const uint GET_RUN_COUNTW = 20;
    public const uint SET_RUN_COUNTA = 21;
    public const uint SET_RUN_COUNTW = 22;
    public const uint INC_RUN_COUNTA = 23;
    public const uint INC_RUN_COUNTW = 24;

    // Custom reply message identifier (commonly used in examples)
    public const uint COPYDATA_QUERYCOMPLETE = 0;
}

public enum TargetMachine : uint
{
    X86 = 1,
    X64 = 2,
    ARM = 3,
    ARM64 = 4
}

public enum BuiltInFilter : uint
{
    Everything = 0,
    Audio = 1,
    Compressed = 2,
    Document = 3,
    Executable = 4,
    Folder = 5,
    Picture = 6,
    Video = 7,
    Custom = 8
}

[Flags]
public enum SearchFlags : uint
{
    None = 0x00000000,
    MatchCase = 0x00000001,
    MatchWholeWord = 0x00000002,
    MatchPath = 0x00000004,
    Regex = 0x00000008,
    MatchAccents = 0x00000010,
    MatchDiacritics = 0x00000010,
    MatchPrefix = 0x00000020,
    MatchSuffix = 0x00000040,
    IgnorePunctuation = 0x00000080,
    IgnoreWhitespace = 0x00000100
}

[Flags]
public enum ItemFlags : uint
{
    None = 0x00000000,
    Folder = 0x00000001,
    Drive = 0x00000002,
    Root = 0x00000002
}

public enum SortType : uint
{
    NameAscending = 1,
    NameDescending = 2,
    PathAscending = 3,
    PathDescending = 4,
    SizeAscending = 5,
    SizeDescending = 6,
    ExtensionAscending = 7,
    ExtensionDescending = 8,
    TypeNameAscending = 9,
    TypeNameDescending = 10,
    DateCreatedAscending = 11,
    DateCreatedDescending = 12,
    DateModifiedAscending = 13,
    DateModifiedDescending = 14,
    AttributesAscending = 15,
    AttributesDescending = 16,
    FileListFilenameAscending = 17,
    FileListFilenameDescending = 18,
    RunCountAscending = 19,
    RunCountDescending = 20,
    DateRecentlyChangedAscending = 21,
    DateRecentlyChangedDescending = 22,
    DateAccessedAscending = 23,
    DateAccessedDescending = 24,
    DateRunAscending = 25,
    DateRunDescending = 26
}

[Flags]
public enum Query2RequestFlags : uint
{
    None = 0x00000000,
    Name = 0x00000001,
    Path = 0x00000002,
    FullPathAndName = 0x00000004,
    Extension = 0x00000008,
    Size = 0x00000010,
    DateCreated = 0x00000020,
    DateModified = 0x00000040,
    DateAccessed = 0x00000080,
    Attributes = 0x00000100,
    FileListFileName = 0x00000200,
    RunCount = 0x00000400,
    DateRun = 0x00000800,
    DateRecentlyChanged = 0x00001000,
    HighlightedName = 0x00002000,

    HighlightedPath = 0x00004000,
    HighlightedFullPathAndName = 0x00008000,

    // Convenience combinations
    Basic = Name | Path,
    All = Name | Path | FullPathAndName | Extension | Size | DateCreated | DateModified |
          DateAccessed | Attributes | RunCount | DateRun | DateRecentlyChanged
}

public enum QueryType
{
    /// <summary>
    /// Automatically choose QUERY1 or QUERY2 based on RequestFlags
    /// </summary>
    Auto,

    /// <summary>
    /// Use QUERY1 - basic name/path search (fastest)
    /// </summary>
    Query1,

    /// <summary>
    /// Use QUERY2 - extended search with metadata
    /// </summary>
    Query2
}