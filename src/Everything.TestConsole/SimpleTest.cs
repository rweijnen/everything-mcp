using Everything.Interop;
using System.Runtime.InteropServices;

namespace Everything.TestConsole;

public static class SimpleTest
{
    public static void TestEverythingDirectly()
    {
        Console.WriteLine("Testing direct Everything communication...");

        // Find Everything window
        var everythingWindow = NativeMethods.FindWindow(Constants.EVERYTHING_IPC_WNDCLASS, null);
        if (everythingWindow == IntPtr.Zero)
        {
            Console.WriteLine("❌ Could not find Everything window");
            return;
        }

        Console.WriteLine($"✅ Found Everything window: 0x{everythingWindow:X}");

        // Test basic version query
        var version = NativeMethods.SendMessage(everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.GET_MAJOR_VERSION, 0);
        Console.WriteLine($"✅ Major version: {version}");

        // Test if database is loaded
        var dbLoaded = NativeMethods.SendMessage(everythingWindow, Constants.EVERYTHING_WM_IPC,
            EverythingIpcCommands.IS_DB_LOADED, 0);
        Console.WriteLine($"✅ Database loaded: {dbLoaded != IntPtr.Zero}");

        // Let's check structure sizes
        Console.WriteLine("\nStructure sizes:");
        Console.WriteLine($"CopyDataStruct: {Marshal.SizeOf<CopyDataStruct>()} bytes");
        Console.WriteLine($"EverythingIpcQueryW: {Marshal.SizeOf<EverythingIpcQueryW>()} bytes");
        Console.WriteLine($"EverythingIpcItemW: {Marshal.SizeOf<EverythingIpcItemW>()} bytes");
        Console.WriteLine($"EverythingIpcListW: {Marshal.SizeOf<EverythingIpcListW>()} bytes");

        Console.WriteLine("\nStructure layout verification complete.");
    }
}