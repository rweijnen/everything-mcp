namespace Everything.Mcp.Configuration;

/// <summary>
/// Configuration settings for file logging
/// </summary>
public class LoggingConfiguration
{
    /// <summary>
    /// Whether file logging is enabled (default: false)
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Log file path (default: logs/everything-mcp.log)
    /// </summary>
    public string LogFilePath { get; set; } = "logs/everything-mcp.log";

    /// <summary>
    /// Minimum log level (default: Information)
    /// Valid values: Verbose, Debug, Information, Warning, Error, Fatal
    /// </summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Rolling interval for log files (default: Day)
    /// Valid values: Infinite, Year, Month, Day, Hour, Minute
    /// </summary>
    public string RollingInterval { get; set; } = "Day";

    /// <summary>
    /// Number of log files to retain (default: 7)
    /// </summary>
    public int RetainedFileCountLimit { get; set; } = 7;
}