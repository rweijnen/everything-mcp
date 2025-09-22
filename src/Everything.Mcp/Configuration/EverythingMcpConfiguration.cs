namespace Everything.Mcp.Configuration;

/// <summary>
/// Main configuration for Everything MCP server
/// </summary>
public class EverythingMcpConfiguration
{
    /// <summary>
    /// Logging configuration section
    /// </summary>
    public LoggingConfiguration Logging { get; set; } = new();

    /// <summary>
    /// Everything client configuration section
    /// </summary>
    public EverythingClientConfiguration EverythingClient { get; set; } = new();
}

/// <summary>
/// Configuration for Everything client behavior
/// </summary>
public class EverythingClientConfiguration
{
    /// <summary>
    /// Enable auto-refresh of Everything database (default: false)
    /// </summary>
    public bool EnableAutoRefresh { get; set; } = false;

    /// <summary>
    /// Refresh interval in minutes (default: 5)
    /// </summary>
    public int RefreshIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Default timeout for queries in milliseconds (default: 10000)
    /// </summary>
    public int DefaultTimeoutMs { get; set; } = 10000;
}