using System.Runtime.InteropServices;
using System.Text;

namespace Everything.Interop;

public class EverythingIpc : IDisposable
{
    private const int DefaultTimeoutMs = 5000;
    private const int MaxQueryLength = 32000;

    private IntPtr _everythingWindow = IntPtr.Zero;
    private bool _disposed = false;

    public EverythingIpc()
    {
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
            queryPtr->ReplyHwnd = (uint)replyHwnd.ToInt32();
            queryPtr->ReplyCopyDataMessage = replyMessage;
            queryPtr->SearchFlags = (uint)options.Flags;
            queryPtr->Offset = options.Offset;
            queryPtr->MaxResults = options.MaxResults;

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
                var result = NativeMethods.SendMessage(_everythingWindow, Constants.WM_COPYDATA,
                    replyHwnd, copyDataPtr);

                if (result == IntPtr.Zero)
                    throw EverythingIpcException.SendMessageFailed("QueryW");
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

    public unsafe SearchResult[] ParseResults(IntPtr data, uint dataSize, bool unicode = true)
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

    private void EnsureEverythingRunning()
    {
        if (!IsEverythingRunning)
        {
            RefreshEverythingWindow();
            if (!IsEverythingRunning)
                throw EverythingIpcException.EverythingNotRunning();
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