using Everything.Client;

namespace Everything.Integration.Tests;

/// <summary>
/// Static context to share a single EverythingClient instance across all test fixtures,
/// ensuring only one Windows message window is created.
/// This mimics how a real MCP server works - single instance handling all requests.
/// </summary>
internal static class SharedTestContext
{
    /// <summary>
    /// The single shared EverythingClient instance used by all tests
    /// </summary>
    public static EverythingClient? SharedEverythingClient { get; set; }
}