using Everything.Client;
using Everything.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Everything.TestConsole;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        // Check for verification mode
        if (args.Length > 0 && args[0] == "--verify")
        {
            return await VerificationProgram.RunVerificationAsync(args.Skip(1).ToArray());
        }

        // Check for metadata verification mode
        if (args.Length > 0 && args[0] == "--metadata")
        {
            return await MetadataVerificationProgram.RunMetadataVerificationAsync(args.Skip(1).ToArray());
        }

        // Check for dual-mode verification
        if (args.Length > 0 && args[0] == "--dual")
        {
            return await DualModeVerificationProgram.RunDualModeVerificationAsync(args.Skip(1).ToArray());
        }

        // Check for QUERY2 debug mode
        if (args.Length > 0 && args[0] == "--debug-query2")
        {
            return await DebugQuery2Program.RunDebugAsync(args.Skip(1).ToArray());
        }

        // Check for benchmark mode
        if (args.Length > 0 && args[0] == "--benchmark")
        {
            return await BenchmarkProgram.RunBenchmarkAsync(args.Skip(1).ToArray());
        }
        try
        {
            var host = CreateHostBuilder(args).Build();
            var app = host.Services.GetRequiredService<TestApplication>();
            return await app.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                services.AddEverythingClient(options =>
                {
                    options.DefaultTimeoutMs = 10000;
                    options.DefaultMaxResults = 100;
                    options.EnableAutoRefresh = true;
                });

                services.AddSingleton<TestApplication>();
            });
}

internal class TestApplication
{
    private readonly IEverythingClient _everythingClient;
    private readonly ILogger<TestApplication> _logger;

    public TestApplication(IEverythingClient everythingClient, ILogger<TestApplication> logger)
    {
        _everythingClient = everythingClient;
        _logger = logger;
    }

    public async Task<int> RunAsync()
    {
        Console.WriteLine("Everything MCP Test Console");
        Console.WriteLine("===========================");
        Console.WriteLine();

        try
        {
            // First run simple direct test
            SimpleTest.TestEverythingDirectly();
            Console.WriteLine();

            await TestConnection();
            await TestBasicSearch();
            await TestAdvancedSearch();
            await RunInteractiveMode();

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test failed");
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private Task TestConnection()
    {
        Console.WriteLine("Testing Everything connection...");

        if (!_everythingClient.IsEverythingRunning)
        {
            Console.WriteLine("❌ Everything is not running. Please start Everything and try again.");
            throw new InvalidOperationException("Everything is not running");
        }

        Console.WriteLine("✅ Everything is running");
        Console.WriteLine($"   Version: {_everythingClient.EverythingVersion}");
        Console.WriteLine($"   Target Machine: {_everythingClient.TargetMachine}");
        Console.WriteLine($"   Database Loaded: {_everythingClient.IsDbLoaded}");
        Console.WriteLine($"   Database Busy: {_everythingClient.IsDbBusy}");
        Console.WriteLine($"   Running as Admin: {_everythingClient.IsAdmin}");
        Console.WriteLine();
        return Task.CompletedTask;
    }

    private async Task TestBasicSearch()
    {
        Console.WriteLine("Testing basic search (*.txt files)...");

        var results = await _everythingClient.SearchAsync("*.txt");

        Console.WriteLine($"✅ Found {results.Length} .txt files");

        if (results.Length > 0)
        {
            Console.WriteLine("   First 5 results:");
            foreach (var result in results.Take(5))
            {
                var type = result.IsFolder ? "📁" : "📄";
                Console.WriteLine($"   {type} {result.FullPath}");
            }
        }
        Console.WriteLine();
    }

    private async Task TestAdvancedSearch()
    {
        Console.WriteLine("Testing advanced search features...");

        // Test file search
        var files = await _everythingClient.SearchFilesAsync("*.exe");
        Console.WriteLine($"✅ Found {files.Length} executable files");

        // Test folder search
        var folders = await _everythingClient.SearchFoldersAsync("Windows");
        Console.WriteLine($"✅ Found {folders.Length} folders containing 'Windows'");

        // Test extension search
        var images = await _everythingClient.SearchByExtensionAsync("jpg");
        Console.WriteLine($"✅ Found {images.Length} .jpg files");

        // Test search with flags
        var caseSearch = await _everythingClient.SearchAsync("README", SearchFlags.MatchCase);
        Console.WriteLine($"✅ Found {caseSearch.Length} files with case-sensitive 'README'");

        Console.WriteLine();
    }

    private async Task RunInteractiveMode()
    {
        Console.WriteLine("Interactive Search Mode");
        Console.WriteLine("Type 'quit' to exit, 'help' for commands");
        Console.WriteLine();

        while (true)
        {
            Console.Write("Search> ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
                continue;

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                ShowHelp();
                continue;
            }

            if (input.StartsWith("status", StringComparison.OrdinalIgnoreCase))
            {
                await ShowStatus();
                continue;
            }

            await ExecuteSearch(input);
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Available commands:");
        Console.WriteLine("  <search query>  - Search for files/folders");
        Console.WriteLine("  status          - Show Everything status");
        Console.WriteLine("  help            - Show this help");
        Console.WriteLine("  quit            - Exit the application");
        Console.WriteLine();
        Console.WriteLine("Search examples:");
        Console.WriteLine("  *.txt          - All text files");
        Console.WriteLine("  file:*.exe     - All executable files");
        Console.WriteLine("  folder:temp    - Folders containing 'temp'");
        Console.WriteLine("  ext:jpg        - All .jpg files");
        Console.WriteLine("  size:>100mb    - Files larger than 100MB");
        Console.WriteLine();
    }

    private Task ShowStatus()
    {
        Console.WriteLine();
        Console.WriteLine("Everything Status:");
        Console.WriteLine($"  Running: {_everythingClient.IsEverythingRunning}");
        Console.WriteLine($"  Version: {_everythingClient.EverythingVersion}");
        Console.WriteLine($"  Database Loaded: {_everythingClient.IsDbLoaded}");
        Console.WriteLine($"  Database Busy: {_everythingClient.IsDbBusy}");
        Console.WriteLine($"  Admin: {_everythingClient.IsAdmin}");
        Console.WriteLine();
        return Task.CompletedTask;
    }

    private async Task ExecuteSearch(string query)
    {
        try
        {
            Console.WriteLine($"Searching for: {query}");
            var startTime = DateTime.UtcNow;

            var results = await _everythingClient.SearchAsync(query);
            var duration = DateTime.UtcNow - startTime;

            Console.WriteLine($"Found {results.Length} results in {duration.TotalMilliseconds:F0}ms");

            if (results.Length > 0)
            {
                var displayCount = Math.Min(results.Length, 20);
                Console.WriteLine($"Showing first {displayCount} results:");

                foreach (var result in results.Take(displayCount))
                {
                    var type = result.IsFolder ? "📁" : "📄";
                    var size = result.Size.HasValue ? $" ({FormatSize(result.Size.Value)})" : "";
                    Console.WriteLine($"  {type} {result.FullPath}{size}");
                }

                if (results.Length > displayCount)
                {
                    Console.WriteLine($"  ... and {results.Length - displayCount} more results");
                }
            }

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Search failed: {ex.Message}");
            Console.WriteLine();
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}