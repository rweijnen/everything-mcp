# Everything MCP Server

A high-performance Model Context Protocol (MCP) server that provides file search capabilities using the Everything Search Engine on Windows.

## Features

- ⚡ Lightning-fast file search using Everything's indexed database
- 🔍 6 specialized search tools for AI coding assistants
- 📊 Optional rich metadata (file sizes, timestamps, attributes)
- 🔒 Works across privilege levels (elevated/non-elevated)

## Prerequisites

- Windows operating system
- [Everything Search Engine](https://www.voidtools.com/) installed and running
- .NET 8.0 SDK (for building from source)

## Installation

### Option 1: Download Release
1. Download the latest release from [GitHub Releases](https://github.com/rweijnen/everything-mcp/releases)
2. Extract to a directory (e.g., `C:\Tools\everything-mcp\`)

### Option 2: Build from Source
```bash
git clone https://github.com/rweijnen/everything-mcp.git
cd everything-mcp
dotnet build --configuration Release
```

## Configuration

### Claude Desktop
Add to `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "everything": {
      "command": "C:\\Tools\\everything-mcp\\everything-mcp.exe"
    }
  }
}
```

### Claude Code
Use the command palette (`Ctrl+Shift+P`):
- Run: `mcp add`
- Name: `everything`
- Command: `C:\Tools\everything-mcp\everything-mcp.exe`

## Configuration

The MCP server can be configured via `appsettings.json` in the same directory as the executable. By default, logging is **disabled**.

### Enable Logging (Optional)

To enable file logging for debugging, create an `appsettings.json` file:

```json
{
  "EverythingMcp": {
    "Logging": {
      "Enabled": true,
      "LogFilePath": "logs/everything-mcp.log",
      "LogLevel": "Information",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 7
    }
  }
}
```

### Configuration Options

- **Logging.Enabled**: Enable file logging (default: `false`)
- **Logging.LogFilePath**: Path for log files (supports environment variables like `%TEMP%`)
- **Logging.LogLevel**: Minimum log level (`Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal`)
- **Logging.RollingInterval**: How often to create new log files (`Day`, `Hour`, etc.)
- **Logging.RetainedFileCountLimit**: Number of old log files to keep

See `appsettings.example.json` for a complete configuration example with comments.

## Available Tools

### 1. `search_files`
General file and folder search with intelligent scoping
- `query` (required): Search query with Everything syntax (wildcards, operators, etc.)
- `scope`: Search scope (default: "current")
  - `"current"`: Current directory only
  - `"recursive"`: Current directory and subdirectories
  - `"system"`: System-wide search
  - `"path:/custom/path"`: Search in specific path
- `include_metadata`: Include file sizes and timestamps (default: false)
- `max_results`: Limit results (default: 100)

### 2. `search_in_project`
Search within a specific project directory
- `project_path` (required): Root directory of the project
- `pattern` (required): Search pattern (wildcards supported)
- `include_metadata`: Include file metadata (default: false)
- `max_results`: Maximum number of results (default: 100)

### 3. `find_executable`
Locate executable files (smart detection based on input)
- `name` (required): Name of the executable
- `exact_match`: Use exact matching (default: auto-detected)
  - Auto-detection: Uses exact match if extension provided or no wildcards
- `max_results`: Maximum number of results (default: 20)

### 4. `find_source_files`
Find source code files by programming language
- `filename` (required): Base filename to search for
- `extensions`: Comma-separated language extensions (e.g., 'cs,js,py')
  - Default extensions by language included automatically
- `include_metadata`: Include file metadata (default: false)
- `max_results`: Maximum number of results (default: 100)

### 5. `search_recent_files`
Find recently modified files
- `hours` (required): Number of hours to look back
- `pattern`: File pattern to filter (default: "*" for all files)
- `include_metadata`: Include file metadata (default: true)
- `max_results`: Maximum number of results (default: 100)

### 6. `find_config_files`
Find configuration files in a project or globally
- `project_path`: Project directory (null for global search)
- `include_metadata`: Include file metadata (default: false)
- `max_results`: Maximum number of results (default: 50)

## Building

```bash
dotnet build --configuration Release
```

## Everything Search Syntax

All tools support Everything's powerful search syntax in the `query` parameter:

### Basic Operators
- **AND**: `file1 file2` (space-separated)
- **OR**: `file1|file2` (pipe-separated)
- **NOT**: `!unwanted` (exclamation mark)
- **Grouping**: `<group1> <group2>`
- **Exact filename**: `exact:"filename.ext"` (exact operator)

### Wildcards
- `*` - Zero or more characters (`*.txt`, `test*`)
- `?` - Single character (`file?.txt`)

### Advanced Features
- **Regex**: `regex:^[A-Z]+\.txt$` (regular expressions)
- **File size**: `size:>1GB`, `size:<100MB`
- **Extensions**: `ext:doc;pdf;txt`
- **Empty files**: `empty:`
- **Name length**: `len:>50`
- **Starts/ends with**: `startwith:test`, `endwith:.log`
- **Count limit**: `count:10`

### Examples
```
*.cs !bin !obj                    # C# files excluding build folders
regex:test.*\.log$                 # Log files starting with "test"
size:>100MB ext:mp4;avi           # Large video files
exact:"notepad.exe"                # Exact filename match
file1|file2 !folder               # Either file1 or file2, not in "folder"
```

## Testing

```bash
# List tools
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project src/Everything.Mcp/

# Search example
echo '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"search_files","arguments":{"query":"*.txt !temp","scope":"system","max_results":5}}}' | dotnet run --project src/Everything.Mcp/
```

## Troubleshooting

- Ensure Everything Search Engine is running
- Verify Everything has finished indexing
- Test queries in Everything's GUI first

## License

Mozilla Public License 2.0

## Acknowledgments

- [Everything Search Engine](https://www.voidtools.com/) by David Carpenter
- [Model Context Protocol](https://modelcontextprotocol.io/) by Anthropic