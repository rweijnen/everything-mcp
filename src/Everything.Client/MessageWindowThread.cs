using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Everything.Interop;

namespace Everything.Client;

/// <summary>
/// Dedicated thread for Windows message operations to ensure proper message pump
/// </summary>
internal class MessageWindowThread : IDisposable
{
    // Use centralized P/Invoke declarations from Everything.Interop
    private static uint MsgWaitForMultipleObjects(uint nCount, IntPtr[] pHandles, bool bWaitAll, uint dwMilliseconds, uint dwWakeMask) =>
        NativeMethods.MsgWaitForMultipleObjects(nCount, pHandles, bWaitAll, dwMilliseconds, dwWakeMask);

    private static bool PeekMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg) =>
        NativeMethods.PeekMessage(out lpMsg, hWnd, wMsgFilterMin, wMsgFilterMax, wRemoveMsg);

    private static bool TranslateMessage(ref Msg lpMsg) => NativeMethods.TranslateMessage(ref lpMsg);
    private static IntPtr DispatchMessage(ref Msg lpMsg) => NativeMethods.DispatchMessage(ref lpMsg);

    private const uint QS_ALLINPUT = NativeMethods.QS_ALLINPUT;
    private const uint WAIT_OBJECT_0 = NativeMethods.WAIT_OBJECT_0;
    private const uint WAIT_FAILED = NativeMethods.WAIT_FAILED;

    private readonly ILogger _logger;
    private readonly Thread _windowThread;
    private readonly ManualResetEventSlim _threadReady = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly ConcurrentQueue<QueryRequest> _queryQueue = new();
    private readonly ManualResetEventSlim _queryAvailable = new();
    private readonly ConcurrentQueue<QueryRequest> _pendingResponses = new();
    private WndProcDelegate? _wndProcDelegate;
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _everythingWindowHandle;
    private bool _disposed = false;

    private class QueryRequest
    {
        public required SearchOptions Options { get; init; }
        public required TaskCompletionSource<SearchResult[]> TaskCompletionSource { get; init; }
        public required CancellationToken CancellationToken { get; init; }
    }

    public MessageWindowThread(ILogger logger, IntPtr everythingWindowHandle)
    {
        _logger = logger;
        _everythingWindowHandle = everythingWindowHandle;
        _windowThread = new Thread(WindowThreadProc)
        {
            IsBackground = false, // Keep the thread alive
            Name = "Everything-MessageWindow"
        };
        _windowThread.SetApartmentState(ApartmentState.STA); // Single Threaded Apartment for Windows operations
        _windowThread.Start();

        // Wait for the window to be created
        if (!_threadReady.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new InvalidOperationException("Failed to initialize message window thread within timeout");
        }

        _logger.LogDebug("MessageWindowThread initialized with window handle: {WindowHandle}", _hwnd);
    }

    public Task<SearchResult[]> QueryAsync(SearchOptions options, CancellationToken cancellationToken)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MessageWindowThread));

        var tcs = new TaskCompletionSource<SearchResult[]>();

        var request = new QueryRequest
        {
            Options = options,
            TaskCompletionSource = tcs,
            CancellationToken = cancellationToken
        };

        _logger.LogDebug("Queuing search request for query: {Query}", options.Query);

        // Add to queue and signal that work is available
        _queryQueue.Enqueue(request);
        _queryAvailable.Set();

        _logger.LogDebug("Query enqueued successfully, queue size: {QueueSize}", _queryQueue.Count);

        return tcs.Task;
    }

    private void WindowThreadProc()
    {
        try
        {
            _logger.LogDebug("Starting message window thread");

            // Create the window on this dedicated thread
            CreateMessageWindow();

            _logger.LogDebug("Message window created successfully, handle: {WindowHandle}", _hwnd);

            // Signal that the thread is ready
            _threadReady.Set();

            // Run the message pump
            RunMessagePump();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message window thread");
            _threadReady.Set(); // Signal even on error so constructor doesn't hang
        }
        finally
        {
            if (_hwnd != IntPtr.Zero)
            {
                DestroyWindow(_hwnd);
                _hwnd = IntPtr.Zero;
            }
            _logger.LogDebug("Message window thread exiting");
        }
    }

    private void CreateMessageWindow()
    {
        const string WindowClassName = "EverythingMcp_MessageWindow";
        var hInstance = GetModuleHandle(null);

        // Create delegate on the window thread to ensure proper lifetime
        _wndProcDelegate = new WndProcDelegate(WndProc);

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
        {
            var error = Marshal.GetLastWin32Error();
            const int ERROR_CLASS_ALREADY_EXISTS = 1410;

            if (error == ERROR_CLASS_ALREADY_EXISTS)
            {
                _logger.LogDebug("Window class already exists, will reuse it");
            }
            else
            {
                _logger.LogError("Failed to register window class. Win32 Error: {Error}", error);
                throw new InvalidOperationException($"Failed to register window class: {error}");
            }
        }
        else
        {
            _logger.LogDebug("Window class registered successfully, atom: {Atom}", classAtom);
        }

        _hwnd = CreateWindowEx(
            0, WindowClassName, "EverythingMcpMessageWindow", 0,
            0, 0, 0, 0,
            HWND_MESSAGE, // Message-only window
            IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogError("Failed to create message window. Win32 Error: {Error}", error);
            throw new InvalidOperationException($"Failed to create message window: {error}");
        }

        _logger.LogDebug("Message window created successfully: {WindowHandle}", _hwnd);

        // Setup message filter for cross-privilege IPC
        ChangeWindowMessageFilter(WM_COPYDATA, MSGFLT_ADD);
    }

    private void RunMessagePump()
    {
        _logger.LogDebug("Starting message pump on dedicated thread");

        // Use proper Win32 event-driven approach
        var cancelHandle = _cancellationTokenSource.Token.WaitHandle;
        var queryHandle = _queryAvailable.WaitHandle;
        var handles = new[] {
            cancelHandle.SafeWaitHandle.DangerousGetHandle(),
            queryHandle.SafeWaitHandle.DangerousGetHandle()
        };

        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            // Process any queued work first
            ProcessQueuedRequests();

            // Wait for cancellation, queries, OR messages (no polling!)
            var waitResult = MsgWaitForMultipleObjects(2, handles, false, unchecked((uint)-1), QS_ALLINPUT);

            if (waitResult == WAIT_FAILED)
            {
                _logger.LogError("MsgWaitForMultipleObjects failed: {Error}", Marshal.GetLastWin32Error());
                break;
            }

            // Check for cancellation (WAIT_OBJECT_0 = first handle)
            if (waitResult == WAIT_OBJECT_0)
            {
                _logger.LogDebug("Cancellation signaled, exiting message pump");
                break;
            }

            // Check for new queries (WAIT_OBJECT_0 + 1 = second handle)
            if (waitResult == WAIT_OBJECT_0 + 1)
            {
                _queryAvailable.Reset();
                // Will process queries in next loop iteration
                continue;
            }

            // Process all pending messages (waitResult == WAIT_OBJECT_0 + 2 means messages available)
            while (PeekMessage(out var msg, _hwnd, 0, 0, 1)) // PM_REMOVE = 1
            {
                if (msg.message == WM_QUIT)
                {
                    _logger.LogDebug("Received WM_QUIT, exiting message pump");
                    return;
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        _logger.LogDebug("Message pump exited");
    }

    private void ProcessQueuedRequests()
    {
        while (_queryQueue.TryDequeue(out var request))
        {
            _logger.LogDebug("Processing queued request for query: {Query}", request.Options.Query);
            ProcessQuery(request);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (msg == WM_COPYDATA)
            {
                ProcessCopyDataMessage(hWnd, wParam, lParam);
                return new IntPtr(1); // Return TRUE to indicate message was processed
            }

            return DefWindowProc(hWnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in WndProc for message: {Message}", msg);
            return DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    private void ProcessQuery(QueryRequest request)
    {
        try
        {
            _logger.LogDebug("Processing query on window thread: {Query}", request.Options.Query);

            using var ipc = new EverythingIpc(_logger);

            // All Everything queries result in WM_COPYDATA responses, so queue for processing
            _pendingResponses.Enqueue(request);
            _logger.LogDebug("Query queued for WM_COPYDATA response (RequiresQuery2: {RequiresQuery2}): {Query}",
                request.Options.RequiresQuery2, request.Options.Query);

            ipc.QueryW(request.Options, _hwnd, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query: {Query}", request.Options.Query);
            request.TaskCompletionSource.SetException(ex);
        }
    }

    private void ProcessCopyDataMessage(IntPtr hWnd, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            // Security: Verify the message comes from Everything window
            _logger.LogDebug("DEBUG: Received WM_COPYDATA from handle {SenderHandle:X}, Everything handle is {EverythingHandle:X}",
                wParam, _everythingWindowHandle);

            if (_everythingWindowHandle == IntPtr.Zero)
            {
                _logger.LogWarning("Received WM_COPYDATA but Everything window handle not set, ignoring message");
                return;
            }

            if (wParam != _everythingWindowHandle)
            {
                _logger.LogWarning("Received WM_COPYDATA from untrusted sender (expected: {ExpectedHandle:X}, actual: {ActualHandle:X}), ignoring message",
                    _everythingWindowHandle, wParam);
                return;
            }

            _logger.LogDebug("Received WM_COPYDATA message from verified Everything window, parsing response data");

            var copyData = Marshal.PtrToStructure<CopyDataStruct>(lParam);
            _logger.LogDebug("CopyData: dwData={DwData}, cbData={CbData}, lpData={LpData}",
                copyData.dwData, copyData.cbData, copyData.lpData);

            if (copyData.dwData == CopyDataMessages.COPYDATA_QUERYCOMPLETE)
            {
                _logger.LogDebug("Received COPYDATA_QUERYCOMPLETE, processing pending response");

                if (_pendingResponses.TryDequeue(out var request))
                {
                    _logger.LogDebug("Found pending request for query: {Query}", request.Options.Query);

                    try
                    {
                        using var ipc = new EverythingIpc(_logger);
                        var results = ipc.ParseResults(copyData.lpData, copyData.cbData, unicode: true, isQuery2: request.Options.RequiresQuery2);
                        _logger.LogDebug("Parsed {Count} results from Everything response", results.Length);
                        request.TaskCompletionSource.SetResult(results);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing Everything response for query: {Query}", request.Options.Query);
                        request.TaskCompletionSource.SetException(ex);
                    }
                }
                else
                {
                    _logger.LogWarning("Received COPYDATA_QUERYCOMPLETE but no pending requests found");
                }
            }
            else
            {
                _logger.LogDebug("Received WM_COPYDATA with dwData={DwData}, not a query complete message", copyData.dwData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WM_COPYDATA message");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _cancellationTokenSource.Cancel();

            if (_hwnd != IntPtr.Zero)
            {
                PostMessage(_hwnd, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }

            if (_windowThread.IsAlive)
            {
                _windowThread.Join(TimeSpan.FromSeconds(5));
            }

            _threadReady.Dispose();
            _queryAvailable.Dispose();
            _cancellationTokenSource.Dispose();

            // Cancel any pending queries
            while (_queryQueue.TryDequeue(out var request))
            {
                request.TaskCompletionSource.TrySetException(new ObjectDisposedException(nameof(MessageWindowThread)));
            }

            // Cancel any pending responses
            while (_pendingResponses.TryDequeue(out var request))
            {
                request.TaskCompletionSource.TrySetException(new ObjectDisposedException(nameof(MessageWindowThread)));
            }
        }
    }

    #region P/Invoke Declarations

    private const uint WM_COPYDATA = 0x004A;
    private const uint WM_USER = 0x0400;
    private const uint WM_QUIT = 0x0012;
    // Use centralized constants and P/Invoke declarations from Everything.Interop
    private const uint MSGFLT_ADD = NativeMethods.MSGFLT_ADD;
    private static readonly IntPtr HWND_MESSAGE = new IntPtr(NativeMethods.HWND_MESSAGE);

    private static ushort RegisterClassEx(ref WndClassEx lpwcx) => NativeMethods.RegisterClassEx(ref lpwcx);
    private static IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam) =>
        NativeMethods.CreateWindowEx(dwExStyle, lpClassName, lpWindowName, dwStyle, x, y, nWidth, nHeight, hWndParent, hMenu, hInstance, lpParam);
    private static IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) => NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    private static bool DestroyWindow(IntPtr hWnd) => NativeMethods.DestroyWindow(hWnd);
    private static IntPtr GetModuleHandle(string? lpModuleName) => NativeMethods.GetModuleHandle(lpModuleName);
    private static int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax) => NativeMethods.GetMessage(out lpMsg, hWnd, wMsgFilterMin, wMsgFilterMax);
    private static bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam) => NativeMethods.PostMessage(hWnd, msg, wParam, lParam);
    private static bool ChangeWindowMessageFilter(uint message, uint dwFlag) => NativeMethods.ChangeWindowMessageFilter(message, dwFlag);

    #endregion
}