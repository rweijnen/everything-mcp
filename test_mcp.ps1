# Test script for Everything MCP Server
Write-Host "🔍 Testing Everything MCP Server" -ForegroundColor Green
Write-Host "===============================`n"

# Build the MCP server
Write-Host "Building MCP server..." -ForegroundColor Yellow
dotnet build src/Everything.Mcp/ --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful!`n" -ForegroundColor Green

# Start the MCP server process
Write-Host "Starting MCP server..." -ForegroundColor Yellow
$mcpProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project src/Everything.Mcp/" -RedirectStandardInput -RedirectStandardOutput -RedirectStandardError -UseNewEnvironment -PassThru -NoNewWindow

Start-Sleep -Seconds 2

# Test JSON-RPC requests
$testRequests = @(
    @{
        jsonrpc = "2.0"
        id = 1
        method = "initialize"
        params = @{}
    },
    @{
        jsonrpc = "2.0"
        id = 2
        method = "tools/list"
    },
    @{
        jsonrpc = "2.0"
        id = 3
        method = "tools/call"
        params = @{
            name = "search_files"
            arguments = @{
                query = "*.txt"
                maxResults = 3
            }
        }
    }
)

foreach ($request in $testRequests) {
    $json = $request | ConvertTo-Json -Depth 10
    Write-Host "Sending: $($request.method)" -ForegroundColor Cyan

    try {
        $mcpProcess.StandardInput.WriteLine($json)
        $mcpProcess.StandardInput.Flush()
        Start-Sleep -Milliseconds 500
    }
    catch {
        Write-Host "❌ Failed to send request: $_" -ForegroundColor Red
    }
}

# Give time for processing
Start-Sleep -Seconds 2

# Clean up
Write-Host "`nCleaning up..." -ForegroundColor Yellow
try {
    $mcpProcess.Kill()
    $mcpProcess.Dispose()
}
catch {
    Write-Host "Process already terminated" -ForegroundColor Gray
}

Write-Host "✅ Test completed!" -ForegroundColor Green