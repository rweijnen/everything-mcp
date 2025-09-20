using Everything.Interop;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace Everything.Client;

internal unsafe class Win32MessageWindow : IDisposable
{
    private const string WindowClassName = "EverythingClient_MessageWindow";
    private readonly ConcurrentDictionary<uint, TaskCompletionSource<SearchResult[]>> _pendingQueries = new();
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
    private struct Msg
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public Point pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
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
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    public Win32MessageWindow()
    {
        _wndProcDelegate = WndProc;
        CreateMessageWindow();
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

    public Task<SearchResult[]> QueryAsync(SearchOptions options, int timeoutMs, CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Win32MessageWindow));

        var queryId = _nextQueryId++;
        var tcs = new TaskCompletionSource<SearchResult[]>();

        _pendingQueries[queryId] = tcs;

        try
        {
            using var ipc = new EverythingIpc();
            ipc.QueryW(options, _hwnd, queryId);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeoutMs));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            combinedCts.Token.Register(() =>
            {
                _pendingQueries.TryRemove(queryId, out _);
                if (timeoutCts.Token.IsCancellationRequested)
                    tcs.TrySetException(EverythingIpcException.TimeoutError("Search"));
                else
                    tcs.TrySetCanceled(cancellationToken);
            });

            return tcs.Task;
        }
        catch
        {
            _pendingQueries.TryRemove(queryId, out _);
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
                var queryId = (uint)copyData.dwData;

                if (_pendingQueries.TryRemove(queryId, out var tcs))
                {
                    try
                    {
                        using var ipc = new EverythingIpc();
                        var results = ipc.ParseResults(copyData.lpData, copyData.cbData, unicode: true);
                        tcs.SetResult(results);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }

                return (IntPtr)1;
            }
            catch
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

            _disposed = true;
        }
    }
}