# Everything MCP Server - Architecture

## Overview

The Everything MCP Server is a Model Context Protocol (MCP) server that provides AI assistants with fast file and folder search capabilities using the Everything Search Engine. It implements a clean, simple architecture using Microsoft's MCP template with a queue-based Everything client.

## Architecture Components

### 1. MCP Protocol Layer (`Program.cs`)
- **Purpose**: MCP JSON-RPC 2.0 server implementation using Microsoft's template
- **Transport**: Standard I/O (stdio) for communication with MCP clients
- **Framework**: .NET 8 with Microsoft.Extensions.Hosting
- **Logging**: Serilog file-only logging (disabled by default)

### 2. MCP Tools Layer (`EverythingMcpTools.cs`)
- **Purpose**: Implements 6 specialized search tools as MCP methods
- **Pattern**: Uses `[McpServerTool]` attributes for auto-discovery
- **Tools**:
  - `search_files` - General file/folder search with Everything syntax
  - `search_in_project` - Project-scoped search with smart filtering
  - `find_executable` - Executable location with exact/broad match modes
  - `find_source_files` - Source code search by programming language
  - `search_recent_files` - Time-based file search with metadata
  - `find_config_files` - Configuration file discovery with grouping

### 3. Everything Client Layer (`Everything.Client`)
- **Purpose**: High-level async API for Everything Search Engine integration
- **Architecture**: Queue-based with dedicated message window thread
- **Threading**: Dedicated UI thread for Windows message pump
- **IPC**: Windows WM_COPYDATA messages for cross-process communication

### 4. Everything Interop Layer (`Everything.Interop`)
- **Purpose**: Low-level Windows IPC and native API bindings
- **Protocols**: QUERY1 (basic) and QUERY2 (metadata) support
- **UIPI**: Cross-privilege communication filters
- **Structures**: Native Windows message structures and Everything data formats

## Message Flow Diagram

```
┌─────────────────┐    JSON-RPC 2.0     ┌─────────────────┐
│   MCP Client    │◄──── (stdio) ─────► │   MCP Server    │
│  (Claude, etc)  │                     │   (Program.cs)  │
└─────────────────┘                     └─────────────────┘
                                                 │
                                                 │ .NET DI
                                                 ▼
                                        ┌─────────────────┐
                                        │ EverythingMcp   │
                                        │     Tools       │
                                        │ (6 search tools)│
                                        └─────────────────┘
                                                 │
                                                 │ async/await
                                                 ▼
                                        ┌─────────────────┐
                                        │ EverythingClient│
                                        │ (queue-based)   │
                                        └─────────────────┘
                                                 │
                                                 │ thread-safe queue
                                                 ▼
                                        ┌─────────────────┐
                                        │ MessageWindow   │
                                        │    Thread       │
                                        │ (Windows UI)    │
                                        └─────────────────┘
                                                 │
                                                 │ WM_COPYDATA
                                                 ▼
                                        ┌─────────────────┐
                                        │   Everything    │
                                        │ Search Engine   │
                                        │  (external)     │
                                        └─────────────────┘
```

## Detailed Message Flow

### 1. MCP Request Processing
```
1. Claude sends JSON-RPC request via stdio
2. MCP Server deserializes request
3. Server routes to appropriate EverythingMcpTools method
4. Tool validates parameters and builds Everything query
```

### 2. Everything Search Execution
```
5. EverythingClient queues search request
6. MessageWindow thread processes queue
7. Thread sends WM_COPYDATA to Everything process
8. Everything returns results via WM_COPYDATA response
9. Client parses native structures into C# objects
```

### 3. Response Processing
```
10. Tool formats results as anonymous objects
11. MCP Server serializes response to JSON
12. Response sent back to Claude via stdio
```

## Key Design Decisions

### Queue-Based Architecture
- **Rationale**: Ensures thread safety and proper Windows message handling
- **Benefits**: Eliminates race conditions, handles concurrent requests
- **Implementation**: Single consumer (UI thread) with multiple producers

### Anonymous Return Objects
- **Rationale**: Flexible response structure without rigid type definitions
- **Configuration**: `JsonSerializerIsReflectionEnabledByDefault=true` for trimming
- **Trade-off**: Performance vs. flexibility (chose flexibility)

### Dedicated Message Window Thread
- **Rationale**: Windows UI requirements for message pumps
- **Benefits**: Proper UIPI filter support, reliable message handling
- **Isolation**: Everything communication isolated from MCP protocol

### File-Only Logging
- **Rationale**: MCP protocol requires clean stdio streams
- **Default**: Disabled (`MinimumLevel.Default: Fatal`)
- **Configuration**: Example config provided for troubleshooting

## Performance Characteristics

- **First Query**: ~100ms (window creation overhead)
- **Subsequent Queries**: 20-100ms depending on result set size
- **QUERY1 (basic)**: 20-30ms typical
- **QUERY2 (metadata)**: 50-100ms typical
- **Concurrent Requests**: Handled via queue, no conflicts

## Deployment

### Single File Executable
- **Size**: 12MB (trimmed)
- **Dependencies**: Everything Search Engine (external)
- **Requirements**: Windows, .NET 8 runtime (self-contained)
- **Configuration**: `appsettings.json` for logging options

### MCP Client Integration
```json
{
  "mcpServers": {
    "everything": {
      "command": "C:\\path\\to\\Everything.Mcp.exe"
    }
  }
}
```

## Error Handling

### Graceful Degradation
- Everything not running: Clear error messages
- Service shutdown: Handles ObjectDisposedException gracefully
- Invalid queries: Everything engine validation with user feedback
- Cross-privilege: UIPI filters enable elevated/non-elevated communication

### Logging Strategy
- Production: Silent operation (Fatal level only)
- Development: Full debug logging available via configuration
- Troubleshooting: User can enable logging by uncommenting config sections

## Security Considerations

### Cross-Privilege Communication
- **UIPI Filters**: Allows communication regardless of elevation levels
- **Message Validation**: Everything engine handles query validation
- **No Credential Exposure**: File system access via Everything (read-only queries)

### MCP Protocol Security
- **Stdio Transport**: No network exposure
- **Process Isolation**: Each client gets dedicated server process
- **Input Validation**: Parameter validation at tool level

### Windows Message Security
- **WM_COPYDATA Validation**: Verifies sender window handle matches Everything process
- **Process Verification**: Only accepts messages from verified Everything window handle
- **Message Structure Validation**: Validates native COPYDATA structure integrity
- **Handle Verification**: Uses `FindWindow` to verify Everything process identity

## Threading Model

```
Main Thread (MCP Protocol)
├── HTTP/JSON Processing
├── Tool Method Execution
└── Everything Client Calls

Message Window Thread (Everything IPC)
├── Windows Message Pump
├── WM_COPYDATA Handling
└── Everything Communication

Background Threads
└── .NET Thread Pool (async/await)
```

## Extension Points

### Adding New Tools
1. Add method with `[McpServerTool]` attribute to `EverythingMcpTools`
2. Use existing `IEverythingClient` methods
3. Return anonymous objects with consistent structure
4. Follow existing error handling patterns

### Enhanced Everything Integration
- **QUERY2 Extensions**: Additional metadata fields
- **Real-time Updates**: Everything change notifications
- **Advanced Filtering**: Custom query builders
- **Performance Optimization**: Connection pooling, caching