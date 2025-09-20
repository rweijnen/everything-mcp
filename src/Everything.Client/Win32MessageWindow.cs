using Everything.Interop;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace Everything.Client;

internal unsafe class Win32MessageWindow : IDisposable
{
    private const string WindowClassName = "EverythingClient_MessageWindow";
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<SearchResult[]>> _pendingQueries = new();
    private readonly ConcurrentDictionary<uint, bool> _pendingQueryTypes = new(); // Track if query is QUERY2
    private uint _nextQueryId = 1;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _disposed = false;
    private readonly WndProcDelegate _wndProcDelegate;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WndClassEx
    {
        public uint cbSize;
        public uint style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public Point pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int x;
        public int y;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("user32.dll")]
    public static extern bool PeekMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    public static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ChangeWindowMessageFilter(uint message, uint dwFlag);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint message, uint action, IntPtr pChangeFilterStruct);

    // Message filter actions
    private const uint MSGFLT_RESET = 0;
    private const uint MSGFLT_ALLOW = 1;
    private const uint MSGFLT_DISALLOW = 2;


    public Win32MessageWindow()
    {
        _wndProcDelegate = WndProc;
        CreateMessageWindow();

        // Allow WM_COPYDATA messages from lower privilege processes (e.g., non-elevated Everything to elevated MCP)
        SetupMessageFilter();

        // Test: Send ourselves a WM_COPYDATA to verify our WndProc works
        // TestSelfMessage();
    }

    private void TestSelfMessage()
    {
        Console.WriteLine("DEBUG: Testing self WM_COPYDATA message...");

        var testData = "Hello from self!";
        var testBytes = System.Text.Encoding.UTF8.GetBytes(testData);

        var memory = Marshal.AllocHGlobal(testBytes.Length);
        try
        {
            Marshal.Copy(testBytes, 0, memory, testBytes.Length);

            var copyData = new CopyDataStruct
            {
                dwData = (IntPtr)999, // Test value
                cbData = (uint)testBytes.Length,
                lpData = memory
            };

            var copyDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<CopyDataStruct>());
            try
            {
                Marshal.StructureToPtr(copyData, copyDataPtr, false);

                var result = SendMessage(_hwnd, 0x004A, _hwnd, copyDataPtr); // WM_COPYDATA = 0x004A
                Console.WriteLine($"DEBUG: Self-message SendMessage result: {result}");
            }
            finally
            {
                Marshal.FreeHGlobal(copyDataPtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    public IntPtr Handle => _hwnd;

    private void CreateMessageWindow()
    {
        var hInstance = GetModuleHandle(null);

        var wndClass = new WndClassEx
        {
            cbSize = (uint)Marshal.SizeOf<WndClassEx>(),
            style = 0,
            lpfnWndProc = _wndProcDelegate,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = hInstance,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero
        };

        var classAtom = RegisterClassEx(ref wndClass);
        if (classAtom == 0)
            throw new InvalidOperationException($"Failed to register window class: {Marshal.GetLastWin32Error()}");

        _hwnd = CreateWindowEx(
            0, WindowClassName, "EverythingClient", 0,
            0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, hInstance, IntPtr.Zero); // HWND_MESSAGE = -3

        if (_hwnd == IntPtr.Zero)
            throw new InvalidOperationException($"Failed to create message window: {Marshal.GetLastWin32Error()}");
    }

    private void SetupMessageFilter()
    {
        try
        {
            // Allow WM_COPYDATA messages from lower privilege processes
            // This is needed when the MCP server runs elevated but Everything runs non-elevated
            bool result = ChangeWindowMessageFilter(Constants.WM_COPYDATA, MSGFLT_ALLOW);

            if (!result)
            {
                // Try the newer API as fallback
                result = ChangeWindowMessageFilterEx(_hwnd, Constants.WM_COPYDATA, MSGFLT_ALLOW, IntPtr.Zero);
            }
        }
        catch (Exception)
        {
            // Ignore message filter setup errors - not critical for basic operation
        }
    }

    public Task<SearchResult[]> QueryAsync(SearchOptions options, int timeoutMs, CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Win32MessageWindow));

        var queryId = _nextQueryId++;
        var tcs = new TaskCompletionSource<SearchResult[]>();

        _pendingQueries[queryId] = tcs;
        _pendingQueryTypes[queryId] = options.RequiresQuery2;

        try
        {
            using var ipc = new EverythingIpc();
            ipc.QueryW(options, _hwnd, queryId);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            combinedCts.Token.Register(() =>
            {
                _pendingQueries.TryRemove(queryId, out _);
                _pendingQueryTypes.TryRemove(queryId, out _);
                if (timeoutCts.Token.IsCancellationRequested)
                    tcs.TrySetException(EverythingIpcException.TimeoutError("Search"));
                else
                    tcs.TrySetCanceled(cancellationToken);
            });

            // Pump messages on current thread while waiting for response
            var startTime = Environment.TickCount;
            while (!tcs.Task.IsCompleted && !combinedCts.Token.IsCancellationRequested)
            {
                if (PeekMessage(out var msg, _hwnd, 0, 0, 1)) // PM_REMOVE = 1, check our specific window
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
                Thread.Sleep(1);

                // Safety timeout
                if (Environment.TickCount - startTime > timeoutMs)
                {
                    break;
                }
            }

            return tcs.Task;
        }
        catch
        {
            _pendingQueries.TryRemove(queryId, out _);
            _pendingQueryTypes.TryRemove(queryId, out _);
            throw;
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Constants.WM_COPYDATA)
        {
            try
            {
                var copyData = Marshal.PtrToStructure<CopyDataStruct>(lParam);

                if (copyData.dwData == CopyDataMessages.COPYDATA_QUERYCOMPLETE)
                {
                    var firstQuery = _pendingQueries.FirstOrDefault();
                    if (firstQuery.Key != 0 && _pendingQueries.TryRemove(firstQuery.Key, out var tcs))
                    {
                        var isQuery2 = _pendingQueryTypes.TryRemove(firstQuery.Key, out var queryType) && queryType;

                        try
                        {
                            using var ipc = new EverythingIpc();
                            var results = ipc.ParseResults(copyData.lpData, copyData.cbData, unicode: true, isQuery2: isQuery2);
                            tcs.SetResult(results);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    }
                }

                return new IntPtr(1); // TRUE
            }
            catch (Exception)
            {
                return IntPtr.Zero;
            }
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }

            var hInstance = GetModuleHandle(null!);
            UnregisterClass(WindowClassName, hInstance);

            foreach (var tcs in _pendingQueries.Values)
            {
                tcs.TrySetException(new ObjectDisposedException(nameof(Win32MessageWindow)));
            }
            _pendingQueries.Clear();
            _pendingQueryTypes.Clear();

            _disposed = true;
        }
    }
}