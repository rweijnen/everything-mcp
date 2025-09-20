using Everything.Client;
using Everything.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Everything.TestConsole;

public static class DualModeVerificationProgram
{
    public static async Task<int> RunDualModeVerificationAsync(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
                services.AddSingleton<IEverythingClient, EverythingClient>();
            })
            .Build();

        var client = host.Services.GetRequiredService<IEverythingClient>();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            if (!client.IsEverythingRunning)
            {
                Console.WriteLine("❌ Everything is not running");
                return 1;
            }

            Console.WriteLine($"🔄 Everything Dual-Mode Query Verification");
            Console.WriteLine($"   Version: {client.EverythingVersion}");
            Console.WriteLine();

            // Test 1: Basic mode (QUERY1) - Fast, name/path only
            Console.WriteLine("⚡ Testing Basic Mode (QUERY1 - Fast):");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var basicResults = await client.SearchBasicAsync("*.txt", cancellationToken: cts.Token);
            sw.Stop();
            Console.WriteLine($"   Found {basicResults.Length} files in {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"   First result: {(basicResults.Length > 0 ? basicResults[0].Name : "None")}");
            Console.WriteLine($"   Metadata available: Size={(basicResults.Length > 0 ? basicResults[0].Size.HasValue : false)}, Date={(basicResults.Length > 0 ? basicResults[0].DateModified.HasValue : false)}");
            Console.WriteLine();

            // Test 2: Extended mode (QUERY2) - Rich metadata
            Console.WriteLine("📊 Testing Extended Mode (QUERY2 - Full metadata):");
            sw.Restart();
            var extendedResults = await client.SearchWithMetadataAsync("*.txt", cancellationToken: cts.Token);
            sw.Stop();
            Console.WriteLine($"   Found {extendedResults.Length} files in {sw.ElapsedMilliseconds}ms");
            if (extendedResults.Length > 0)
            {
                var file = extendedResults[0];
                Console.WriteLine($"   First result: {file.Name}");
                Console.WriteLine($"   Size: {file.Size?.ToString("N0") ?? "Not available"} bytes");
                Console.WriteLine($"   Created: {file.DateCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not available"}");
                Console.WriteLine($"   Modified: {file.DateModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not available"}");
                Console.WriteLine($"   Accessed: {file.DateAccessed?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not available"}");
                Console.WriteLine($"   Attributes: 0x{file.Attributes:X}");
            }
            Console.WriteLine();

            // Test 3: Selective metadata (QUERY2 with specific fields)
            Console.WriteLine("🎯 Testing Selective Mode (QUERY2 - Size + Date only):");
            sw.Restart();
            var selectiveResults = await client.SearchWithMetadataAsync("*.txt",
                Query2RequestFlags.Name | Query2RequestFlags.Path | Query2RequestFlags.Size | Query2RequestFlags.DateModified,
                cancellationToken: cts.Token);
            sw.Stop();
            Console.WriteLine($"   Found {selectiveResults.Length} files in {sw.ElapsedMilliseconds}ms");
            if (selectiveResults.Length > 0)
            {
                var file = selectiveResults[0];
                Console.WriteLine($"   First result: {file.Name}");
                Console.WriteLine($"   Size: {file.Size?.ToString("N0") ?? "Not available"} bytes");
                Console.WriteLine($"   Modified: {file.DateModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not available"}");
                Console.WriteLine($"   Created: {file.DateCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not available"} (should be empty)");
            }
            Console.WriteLine();

            // Test 4: Auto mode selection
            Console.WriteLine("🤖 Testing Auto Mode (QUERY1 vs QUERY2 selection):");

            var autoBasic = new SearchOptions("*.txt", QueryType: QueryType.Auto); // Should choose QUERY1
            Console.WriteLine($"   Auto with basic flags: Uses {(autoBasic.RequiresQuery2 ? "QUERY2" : "QUERY1")}");

            var autoExtended = new SearchOptions("*.txt", RequestFlags: Query2RequestFlags.All, QueryType: QueryType.Auto); // Should choose QUERY2
            Console.WriteLine($"   Auto with metadata flags: Uses {(autoExtended.RequiresQuery2 ? "QUERY2" : "QUERY1")}");

            Console.WriteLine("\n✅ Dual-mode verification complete!");
            Console.WriteLine("   🚀 Use SearchBasicAsync() for fast name/path searches");
            Console.WriteLine("   📊 Use SearchWithMetadataAsync() for rich file information");
            Console.WriteLine("   🤖 Use SearchAsync() with Auto mode for intelligent selection");

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Dual-mode verification failed: {ex.Message}");
            return 1;
        }
    }
}