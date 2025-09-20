namespace Everything.Interop;

public class EverythingIpcException : Exception
{
    public EverythingIpcException() : base() { }

    public EverythingIpcException(string message) : base(message) { }

    public EverythingIpcException(string message, Exception innerException) : base(message, innerException) { }

    public static EverythingIpcException EverythingNotRunning() =>
        new("Everything is not running. Please start Everything and try again.");

    public static EverythingIpcException WindowNotFound() =>
        new("Could not find Everything window. Ensure Everything is running.");

    public static EverythingIpcException SendMessageFailed(string operation) =>
        new($"Failed to send message to Everything for operation: {operation}");

    public static EverythingIpcException InvalidResponse(string operation) =>
        new($"Received invalid response from Everything for operation: {operation}");

    public static EverythingIpcException TimeoutError(string operation) =>
        new($"Timeout occurred while communicating with Everything for operation: {operation}");

    public static EverythingIpcException MemoryAllocationFailed() =>
        new("Failed to allocate memory for IPC communication");

    public static EverythingIpcException QueryTooLarge(int maxSize) =>
        new($"Query is too large. Maximum size is {maxSize} characters.");
}