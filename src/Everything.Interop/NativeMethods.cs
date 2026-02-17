using System.Runtime.InteropServices;

namespace Everything.Interop;

/// <summary>
/// Delegate for window procedure callback.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.StdCall)]
public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

public static class NativeMethods
{
    private const string User32 = "user32.dll";

    [DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(
        [MarshalAs(UnmanagedType.LPWStr)] string? lpClassName,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpWindowName);

    [DllImport(User32, SetLastError = true)]
    public static extern IntPtr SendMessage(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport(User32, SetLastError = true)]
    public static extern IntPtr SendMessage(
        IntPtr hWnd,
        uint msg,
        uint wParam,
        uint lParam);

    [DllImport(User32, SetLastError = true)]
    public static extern bool SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport(User32, SetLastError = true)]
    public static extern uint RegisterWindowMessage(
        [MarshalAs(UnmanagedType.LPWStr)] string lpString);

    [DllImport(User32, SetLastError = true)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport(User32, SetLastError = true)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport(User32, SetLastError = true)]
    public static extern int GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    // Message pump P/Invoke declarations
    [DllImport(User32, SetLastError = true)]
    public static extern uint MsgWaitForMultipleObjects(uint nCount, IntPtr[] pHandles, bool bWaitAll, uint dwMilliseconds, uint dwWakeMask);

    [DllImport(User32)]
    public static extern bool PeekMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport(User32)]
    public static extern bool TranslateMessage(ref Msg lpMsg);

    [DllImport(User32)]
    public static extern IntPtr DispatchMessage(ref Msg lpMsg);

    [DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassEx(ref WndClassEx lpwcx);

    [DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu,
        IntPtr hInstance, IntPtr lpParam);

    [DllImport(User32, SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport(User32, SetLastError = true)]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport(User32)]
    public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport(User32, SetLastError = true)]
    public static extern bool ChangeWindowMessageFilter(uint message, uint dwFlag);

    [DllImport(User32, SetLastError = true)]
    public static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint message, uint action, IntPtr pChangeFilterStruct);

    [DllImport(User32)]
    public static extern int GetMessage(out Msg lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport(User32)]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LocalAlloc(uint uFlags, IntPtr uBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LocalFree(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LocalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool LocalUnlock(IntPtr hMem);

    public const uint LMEM_FIXED = 0x0000;
    public const uint LMEM_ZEROINIT = 0x0040;
    public const uint LHND = LMEM_FIXED | LMEM_ZEROINIT;

    public const uint SMTO_ABORTIFHUNG = 0x0002;
    public const uint SMTO_BLOCK = 0x0001;
    public const uint SMTO_NORMAL = 0x0000;
    public const uint SMTO_NOTIMEOUTIFNOTHUNG = 0x0008;

    // Message pump constants
    public const uint QS_ALLINPUT = 0x04FF;
    public const uint WAIT_OBJECT_0 = 0x00000000;
    public const uint WAIT_TIMEOUT = 0x00000102;
    public const uint WAIT_FAILED = 0xFFFFFFFF;

    // Window constants
    public const int HWND_MESSAGE = -3;

    // Message filter constants
    public const uint MSGFLT_ADD = 1;

    public static class ShowWindow
    {
        public const uint SW_HIDE = 0;
        public const uint SW_SHOWNORMAL = 1;
        public const uint SW_NORMAL = 1;
        public const uint SW_SHOWMINIMIZED = 2;
        public const uint SW_SHOWMAXIMIZED = 3;
        public const uint SW_MAXIMIZE = 3;
        public const uint SW_SHOWNOACTIVATE = 4;
        public const uint SW_SHOW = 5;
        public const uint SW_MINIMIZE = 6;
        public const uint SW_SHOWMINNOACTIVE = 7;
        public const uint SW_SHOWNA = 8;
        public const uint SW_RESTORE = 9;
        public const uint SW_SHOWDEFAULT = 10;
        public const uint SW_FORCEMINIMIZE = 11;
    }
}