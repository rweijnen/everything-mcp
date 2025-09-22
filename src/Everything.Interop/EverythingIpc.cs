using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Everything.Interop;

public class EverythingIpc : IDisposable
{
    private const int DefaultTimeoutMs = 5000;
    private const int MaxQueryLength = 32000;

    private IntPtr _everythingWindow = IntPtr.Zero;
    private bool _disposed = false;
    private readonly ILogger? _logger;

    public EverythingIpc(ILogger? logger = null)
    {
        _logger = logger;
        RefreshEverythingWindow();
    }

    public bool IsEverythingRunning => _everythingWindow != IntPtr.Zero &&
                                      NativeMethods.IsWindow(_everythingWindow);

    public void RefreshEverythingWindow()
    {
        _everythingWindow = NativeMethods.FindWindow(Constants.EVERYTHING_IPC_WNDCLASS, null);
    }

    public uint GetMajorVersion()
    {
        EnsureEverythingRunning();
        var result = NativeMethods.SendMessage(_everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.GET_MAJOR_VERSION, 0);
        return (uint)result.ToInt32();
    }

    public uint GetMinorVersion()
    {
        EnsureEverythingRunning();
        var result = NativeMethods.SendMessage(_everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.GET_MINOR_VERSION, 0);
        return (uint)result.ToInt32();
    }

    public uint GetRevision()
    {
        EnsureEverythingRunning();
        var result = NativeMethods.SendMessage(_everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.GET_REVISION, 0);
        return (uint)result.ToInt32();
    }

    public uint GetBuildNumber()
    {
        EnsureEverythingRunning();
        var result = NativeMethods.SendMessage(_everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.GET_BUILD_NUMBER, 0);
        return (uint)result.ToInt32();
    }

    public string GetVersionString()
    {
        var major = GetMajorVersion();
        var minor = GetMinorVersion();
        var revision = GetRevision();
        var build = GetBuildNumber();
        return $"{major}.{minor}.{revision}.{build}";
    }

    public TargetMachine GetTargetMachine()
    {
        EnsureEverythingRunning();
        var result = NativeMethods.SendMessage(_everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.GET_TARGET_MACHINE, 0);
        return (TargetMachine)result.ToInt32();
    }

    public bool IsDbLoaded()
    {
        EnsureEverythingRunning();
        var result = NativeMethods.SendMessage(_everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.IS_DB_LOADED, 0);
        return result.ToInt32() != 0;
    }

    public bool IsDbBusy()
    {
        EnsureEverythingRunning();
        var result = NativeMethods.SendMessage(_everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.IS_DB_BUSY, 0);
        return result.ToInt32() != 0;
    }

    public bool IsAdmin()
    {
        EnsureEverythingRunning();
        var result = NativeMethods.SendMessage(_everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.IS_ADMIN, 0);
        return result.ToInt32() != 0;
    }

    public void RebuildDb()
    {
        EnsureEverythingRunning();
        NativeMethods.SendMessage(_everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.REBUILD_DB, 0);
    }

    public void SaveDb()
    {
        EnsureEverythingRunning();
        NativeMethods.SendMessage(_everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.SAVE_DB, 0);
    }

    public unsafe SearchResult[] QueryW(SearchOptions options, IntPtr replyHwnd, uint replyMessage)
    {
        EnsureEverythingRunning();

        if (string.IsNullOrEmpty(options.Query))
            throw new ArgumentException("Query cannot be null or empty", nameof(options));

        if (options.Query.Length > MaxQueryLength)
            throw EverythingIpcException.QueryTooLarge(MaxQueryLength);

        // Determine if we need QUERY2 based on options
        if (options.RequiresQuery2)
        {
            return Query2W(options, replyHwnd, replyMessage);
        }

        // Use QUERY1 for basic searches
        var queryLength = options.Query.Length;
        var structSize = sizeof(EverythingIpcQueryW);
        var totalSize = structSize - sizeof(char) + (queryLength * sizeof(char)) + sizeof(char);

        var queryBytes = Encoding.Unicode.GetBytes(options.Query + '\0');

        var memory = NativeMethods.LocalAlloc(NativeMethods.LHND, (IntPtr)totalSize);
        if (memory == IntPtr.Zero)
            throw EverythingIpcException.MemoryAllocationFailed();

        try
        {
            var queryPtr = (EverythingIpcQueryW*)memory.ToPointer();
            queryPtr->ReplyHwnd = (uint)replyHwnd.ToInt32();  // Cast to 32-bit DWORD
            queryPtr->ReplyCopyDataMessage = CopyDataMessages.COPYDATA_QUERYCOMPLETE;
            queryPtr->SearchFlags = (uint)options.Flags;
            queryPtr->Offset = options.Offset;
            queryPtr->MaxResults = Constants.EVERYTHING_IPC_ALLRESULTS; // Try with ALL results first

            Marshal.Copy(queryBytes, 0, (IntPtr)queryPtr->SearchString, queryBytes.Length);

            var copyData = new CopyDataStruct
            {
                dwData = (IntPtr)CopyDataMessages.COPYDATAQUERYW,
                cbData = (uint)totalSize,
                lpData = memory
            };

            var copyDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<CopyDataStruct>());
            try
            {
                Marshal.StructureToPtr(copyData, copyDataPtr, false);

                _logger?.LogDebug("Sending WM_COPYDATA message to Everything window {WindowHandle} for query: {Query}",
                    _everythingWindow, options.Query);

                var result = NativeMethods.SendMessage(_everythingWindow, Constants.WM_COPYDATA,
                    replyHwnd, copyDataPtr);

                _logger?.LogDebug("SendMessage returned: {Result} for query: {Query}", result, options.Query);

                if (result == IntPtr.Zero)
                {
                    _logger?.LogError("SendMessage failed (returned 0) for query: {Query}", options.Query);
                    throw EverythingIpcException.SendMessageFailed("QueryW");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(copyDataPtr);
            }

            return Array.Empty<SearchResult>();
        }
        finally
        {
            NativeMethods.LocalFree(memory);
        }
    }

    public unsafe SearchResult[] Query2W(SearchOptions options, IntPtr replyHwnd, uint replyMessage)
    {
        EnsureEverythingRunning();

        if (string.IsNullOrEmpty(options.Query))
            throw new ArgumentException("Query cannot be null or empty", nameof(options));

        if (options.Query.Length > MaxQueryLength)
            throw EverythingIpcException.QueryTooLarge(MaxQueryLength);

        var queryLength = options.Query.Length;
        var structSize = sizeof(EverythingIpcQuery2W);
        var totalSize = structSize - sizeof(char) + (queryLength * sizeof(char)) + sizeof(char);

        var queryBytes = Encoding.Unicode.GetBytes(options.Query + '\0');

        var memory = NativeMethods.LocalAlloc(NativeMethods.LHND, (IntPtr)totalSize);
        if (memory == IntPtr.Zero)
            throw EverythingIpcException.MemoryAllocationFailed();

        try
        {
            var queryPtr = (EverythingIpcQuery2W*)memory.ToPointer();
            queryPtr->ReplyHwnd = (uint)replyHwnd.ToInt32();
            queryPtr->ReplyCopyDataMessage = CopyDataMessages.COPYDATA_QUERYCOMPLETE;
            queryPtr->SearchFlags = (uint)options.Flags;
            queryPtr->Offset = options.Offset;
            queryPtr->MaxResults = options.MaxResults;
            queryPtr->RequestFlags = (uint)options.RequestFlags;
            queryPtr->SortType = (uint)options.Sort;

            Marshal.Copy(queryBytes, 0, (IntPtr)queryPtr->SearchString, queryBytes.Length);

            var copyData = new CopyDataStruct
            {
                dwData = (IntPtr)CopyDataMessages.COPYDATA_QUERY2W,
                cbData = (uint)totalSize,
                lpData = memory
            };

            var copyDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<CopyDataStruct>());
            try
            {
                Marshal.StructureToPtr(copyData, copyDataPtr, false);

                var result = NativeMethods.SendMessage(_everythingWindow, Constants.WM_COPYDATA,
                    replyHwnd, copyDataPtr);

                if (result == IntPtr.Zero)
                {
                    throw EverythingIpcException.SendMessageFailed("Query2W");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(copyDataPtr);
            }

            return Array.Empty<SearchResult>();
        }
        finally
        {
            NativeMethods.LocalFree(memory);
        }
    }

    public unsafe SearchResult[] ParseResults(IntPtr data, uint dataSize, bool unicode = true, bool isQuery2 = false)
    {
        if (isQuery2)
        {
            return ParseQuery2Results(data, dataSize, unicode);
        }

        return ParseQuery1Results(data, dataSize, unicode);
    }

    private unsafe SearchResult[] ParseQuery1Results(IntPtr data, uint dataSize, bool unicode = true)
    {
        if (data == IntPtr.Zero || dataSize == 0)
            return Array.Empty<SearchResult>();

        var results = new List<SearchResult>();

        if (unicode)
        {
            var list = (EverythingIpcListW*)data.ToPointer();
            var listPtr = (byte*)data.ToPointer();

            for (uint i = 0; i < list->NumItems; i++)
            {
                var itemPtr = (EverythingIpcItemW*)((byte*)&list->Items + i * sizeof(EverythingIpcItemW));

                var namePtr = (char*)(listPtr + itemPtr->FilenameOffset);
                var pathPtr = (char*)(listPtr + itemPtr->PathOffset);

                var name = new string(namePtr);
                var path = new string(pathPtr);
                var fullPath = Path.Combine(path, name);

                results.Add(new SearchResult(
                    Name: name,
                    Path: path,
                    FullPath: fullPath,
                    Flags: (ItemFlags)itemPtr->Flags));
            }
        }
        else
        {
            var list = (EverythingIpcListA*)data.ToPointer();
            var listPtr = (byte*)data.ToPointer();

            for (uint i = 0; i < list->NumItems; i++)
            {
                var itemPtr = (EverythingIpcItemA*)((byte*)&list->Items + i * sizeof(EverythingIpcItemA));

                var namePtr = (byte*)(listPtr + itemPtr->FilenameOffset);
                var pathPtr = (byte*)(listPtr + itemPtr->PathOffset);

                var name = Marshal.PtrToStringAnsi((IntPtr)namePtr) ?? string.Empty;
                var path = Marshal.PtrToStringAnsi((IntPtr)pathPtr) ?? string.Empty;
                var fullPath = Path.Combine(path, name);

                results.Add(new SearchResult(
                    Name: name,
                    Path: path,
                    FullPath: fullPath,
                    Flags: (ItemFlags)itemPtr->Flags));
            }
        }

        return results.ToArray();
    }

    private unsafe SearchResult[] ParseQuery2Results(IntPtr data, uint dataSize, bool unicode = true)
    {
        if (data == IntPtr.Zero || dataSize < sizeof(EverythingIpcList2W))
            return [];

        var listPtr = (EverythingIpcList2W*)data;
        var list = *listPtr;

        if (list.NumItems == 0)
            return [];

        var results = new List<SearchResult>((int)list.NumItems);

        // Items array starts right after the list header
        var itemsStartPtr = (byte*)data + sizeof(EverythingIpcList2W);

        for (uint i = 0; i < list.NumItems; i++)
        {
            var itemPtr = (EverythingIpcItem2W*)(itemsStartPtr + (i * sizeof(EverythingIpcItem2W)));
            var item = *itemPtr;

            // Validate data offset is within bounds
            if (item.DataOffset >= dataSize)
                continue;

            // Calculate data pointer from the start of the list structure
            var dataPtr = (byte*)data + item.DataOffset;
            var remainingSize = dataSize - item.DataOffset;

            var result = ParseQuery2Item(dataPtr, remainingSize, list.RequestFlags, (ItemFlags)item.Flags);
            results.Add(result);

        }

        return results.ToArray();
    }

    private unsafe SearchResult ParseQuery2Item(byte* dataPtr, uint remainingSize, uint requestFlags, ItemFlags flags)
    {
        var name = string.Empty;
        var path = string.Empty;
        var fullPath = string.Empty;
        long? size = null;
        DateTime? dateCreated = null;
        DateTime? dateModified = null;
        DateTime? dateAccessed = null;
        uint? attributes = null;
        uint? runCount = null;
        DateTime? dateRun = null;

        var offset = 0;

        // Parse fields based on request flags in the order they appear in the data
        if ((requestFlags & (uint)Query2RequestFlags.Name) != 0)
        {
            if (offset + sizeof(uint) > remainingSize) return CreateEmptyResult(flags);

            var nameLength = *(uint*)(dataPtr + offset);
            offset += sizeof(uint);

            if (nameLength > 0 && nameLength < 32768 && offset + (nameLength + 1) * sizeof(char) <= remainingSize)
            {
                var namePtr = (char*)(dataPtr + offset);
                name = new string(namePtr, 0, (int)nameLength);
            }

            offset += (int)(nameLength + 1) * sizeof(char);
        }

        if ((requestFlags & (uint)Query2RequestFlags.Path) != 0)
        {
            if (offset + sizeof(uint) > remainingSize) return CreateEmptyResult(flags);

            var pathLength = *(uint*)(dataPtr + offset);
            offset += sizeof(uint);

            if (pathLength > 0 && pathLength < 32768 && offset + (pathLength + 1) * sizeof(char) <= remainingSize)
            {
                var pathPtr = (char*)(dataPtr + offset);
                path = new string(pathPtr, 0, (int)pathLength);
            }

            offset += (int)(pathLength + 1) * sizeof(char);
        }

        if ((requestFlags & (uint)Query2RequestFlags.FullPathAndName) != 0)
        {
            if (offset + sizeof(uint) > remainingSize) return CreateEmptyResult(flags);
            var fullPathLength = *(uint*)(dataPtr + offset);
            offset += sizeof(uint);

            if (fullPathLength > 0 && fullPathLength < 32768 && offset + fullPathLength * sizeof(char) <= remainingSize)
            {
                fullPath = new string((char*)(dataPtr + offset), 0, (int)fullPathLength);
            }
            offset += (int)fullPathLength * sizeof(char);
        }

        if ((requestFlags & (uint)Query2RequestFlags.Extension) != 0)
        {
            if (offset + sizeof(uint) > remainingSize) return CreateEmptyResult(flags);
            var extLength = *(uint*)(dataPtr + offset);
            offset += sizeof(uint);
            // Skip extension for now
            if (offset + extLength * sizeof(char) <= remainingSize)
            {
                offset += (int)extLength * sizeof(char);
            }
        }

        if ((requestFlags & (uint)Query2RequestFlags.Size) != 0)
        {
            if (offset + sizeof(long) > remainingSize) return CreateEmptyResult(flags);
            size = *(long*)(dataPtr + offset);
            offset += sizeof(long);
        }

        if ((requestFlags & (uint)Query2RequestFlags.DateCreated) != 0)
        {
            if (offset + sizeof(long) > remainingSize) return CreateEmptyResult(flags);
            var fileTime = *(long*)(dataPtr + offset);
            dateCreated = TryParseFileTime(fileTime);
            offset += sizeof(long);
        }

        if ((requestFlags & (uint)Query2RequestFlags.DateModified) != 0)
        {
            if (offset + sizeof(long) > remainingSize) return CreateEmptyResult(flags);
            var fileTime = *(long*)(dataPtr + offset);
            dateModified = TryParseFileTime(fileTime);
            offset += sizeof(long);
        }

        if ((requestFlags & (uint)Query2RequestFlags.DateAccessed) != 0)
        {
            if (offset + sizeof(long) > remainingSize) return CreateEmptyResult(flags);
            var fileTime = *(long*)(dataPtr + offset);
            dateAccessed = TryParseFileTime(fileTime);
            offset += sizeof(long);
        }

        if ((requestFlags & (uint)Query2RequestFlags.Attributes) != 0)
        {
            if (offset + sizeof(uint) > remainingSize) return CreateEmptyResult(flags);
            attributes = *(uint*)(dataPtr + offset);
            offset += sizeof(uint);
        }

        if ((requestFlags & (uint)Query2RequestFlags.RunCount) != 0)
        {
            if (offset + sizeof(uint) > remainingSize) return CreateEmptyResult(flags);
            runCount = *(uint*)(dataPtr + offset);
            offset += sizeof(uint);
        }

        if ((requestFlags & (uint)Query2RequestFlags.DateRun) != 0)
        {
            if (offset + sizeof(long) > remainingSize) return CreateEmptyResult(flags);
            var fileTime = *(long*)(dataPtr + offset);
            dateRun = TryParseFileTime(fileTime);
            offset += sizeof(long);
        }

        // Use fullPath if available, otherwise combine path and name
        var resultFullPath = !string.IsNullOrEmpty(fullPath) ? fullPath :
                           (!string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(name)) ?
                           Path.Combine(path, name) : (name ?? string.Empty);


        return new SearchResult(
            Name: name,
            Path: path,
            FullPath: resultFullPath,
            Flags: flags,
            Size: size,
            DateCreated: dateCreated,
            DateModified: dateModified,
            DateAccessed: dateAccessed,
            Attributes: attributes,
            RunCount: runCount,
            DateRun: dateRun);
    }

    private static DateTime? TryParseFileTime(long fileTime)
    {
        try
        {
            // Zero or negative means no valid time
            if (fileTime <= 0)
                return null;

            // Convert from UTC FILETIME to local DateTime
            // DateTime.FromFileTime automatically converts UTC to local time
            return DateTime.FromFileTime(fileTime);
        }
        catch (ArgumentOutOfRangeException)
        {
            // Invalid file time - probably corrupted data or extreme dates
            return null;
        }
    }

    private static SearchResult CreateEmptyResult(ItemFlags flags)
    {
        return new SearchResult(
            Name: string.Empty,
            Path: string.Empty,
            FullPath: string.Empty,
            Flags: flags);
    }

    private void EnsureEverythingRunning()
    {
        _logger?.LogDebug("Checking if Everything is running...");

        if (!IsEverythingRunning)
        {
            _logger?.LogDebug("Everything not found, refreshing window handle...");
            RefreshEverythingWindow();

            if (!IsEverythingRunning)
            {
                _logger?.LogError("Everything.exe is not running or not responding. Window handle: {WindowHandle}", _everythingWindow);
                throw EverythingIpcException.EverythingNotRunning();
            }

            _logger?.LogDebug("Everything found after refresh. Window handle: {WindowHandle}", _everythingWindow);
        }
        else
        {
            _logger?.LogDebug("Everything is running. Window handle: {WindowHandle}", _everythingWindow);
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
            _disposed = true;
        }
    }
}