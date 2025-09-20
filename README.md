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

## Available Tools

### 1. `search_files`
General file and folder search
- `query` (required): Search query with Everything syntax (wildcards, operators, etc.)
- `includeMetadata`: Include file sizes and timestamps
- `maxResults`: Limit results

### 2. `search_in_project`
Search within a project directory
- `projectPath` (required): Project root directory
- `query` (required): File pattern or name
- `fileTypes`: Extensions to include
- `excludeDirs`: Directories to skip (default: node_modules, .git, bin, obj)

### 3. `find_executable`
Locate executables (like `where` command)
- `name` (required): Executable name
- `exactMatch`: Exact filename match only (default: true)

### 4. `find_source_files`
Find source code by language
- `language`: csharp, javascript, python, java, cpp, etc.
- `extensions`: Custom file extensions
- `directory`: Search within specific directory

### 5. `search_recent_files`
Find recently modified files
- `hours`: Hours to look back (default: 24)
- `filePattern`: Filter by pattern
- `directory`: Limit to directory

### 6. `find_config_files`
Find configuration files
- `configType`: npm, dotnet, git, vscode, docker, build, env, all, custom
- `customPattern`: For custom config type
- `directory`: Limit to directory

## Building

```bash
dotnet build --configuration Release
```

## Testing

```bash
# List tools
echo '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' | dotnet run --project src/Everything.Mcp/

# Search example
echo '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"search_files","arguments":{"query":"*.txt","maxResults":5}}}' | dotnet run --project src/Everything.Mcp/
```

## Troubleshooting

- Ensure Everything Search Engine is running
- Verify Everything has finished indexing
- Test queries in Everything's GUI first

## License

MIT License

## Acknowledgments

- [Everything Search Engine](https://www.voidtools.com/) by David Carpenter
- [Model Context Protocol](https://modelcontextprotocol.io/) by Anthropic