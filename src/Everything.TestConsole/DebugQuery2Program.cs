using Everything.Client;
using Everything.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace Everything.TestConsole;

public static class DebugQuery2Program
{
    public static async Task<int> RunDebugAsync(string[] args)
    {
        Console.WriteLine("🔍 QUERY2 Debug Analysis");
        Console.WriteLine("========================\n");

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<EverythingClient>();
        var options = Options.Create(new EverythingClientOptions());

        using var client = new EverythingClient(options, logger);

        try
        {
            // Test with a simple query that should return files
            var query = "*.txt";
            Console.WriteLine($"Testing QUERY2 with: {query}");
            Console.WriteLine("Requesting: Name + Path + Size + Date Modified\n");

            var results = await client.SearchWithMetadataAsync(
                query,
                Query2RequestFlags.Name | Query2RequestFlags.Path | Query2RequestFlags.Size | Query2RequestFlags.DateModified,
                SearchFlags.None,
                CancellationToken.None);

            Console.WriteLine($"Results returned: {results.Length}");

            if (results.Length > 0)
            {
                Console.WriteLine("\nDetailed analysis of first 3 results:");
                for (int i = 0; i < Math.Min(3, results.Length); i++)
                {
                    var result = results[i];
                    Console.WriteLine($"\nResult {i + 1}:");
                    Console.WriteLine($"  Name: '{result.Name}' (length: {result.Name?.Length ?? 0})");
                    Console.WriteLine($"  Path: '{result.Path}' (length: {result.Path?.Length ?? 0})");
                    Console.WriteLine($"  FullPath: '{result.FullPath}' (length: {result.FullPath?.Length ?? 0})");
                    Console.WriteLine($"  Size: {result.Size} bytes");
                    Console.WriteLine($"  DateModified: {result.DateModified}");
                    Console.WriteLine($"  Flags: {result.Flags}");
                }

                // Analyze the problem
                Console.WriteLine("\n📊 Analysis:");
                var emptyNames = results.Count(r => string.IsNullOrEmpty(r.Name));
                var emptyPaths = results.Count(r => string.IsNullOrEmpty(r.Path));
                var withSize = results.Count(r => r.Size.HasValue);
                var withDate = results.Count(r => r.DateModified.HasValue);

                Console.WriteLine($"  Empty names: {emptyNames}/{results.Length}");
                Console.WriteLine($"  Empty paths: {emptyPaths}/{results.Length}");
                Console.WriteLine($"  With size: {withSize}/{results.Length}");
                Console.WriteLine($"  With date: {withDate}/{results.Length}");

                if (emptyNames > 0 || emptyPaths > 0)
                {
                    Console.WriteLine("\n⚠️  STRING PARSING ISSUE DETECTED!");
                    Console.WriteLine("   - Metadata (size/date) is working");
                    Console.WriteLine("   - String fields (name/path) are empty");
                    Console.WriteLine("   - This suggests string length interpretation is incorrect");
                }
            }
            else
            {
                Console.WriteLine("❌ No results returned - this is unexpected for *.txt");
            }

            // Compare with QUERY1 to verify the issue
            Console.WriteLine("\n🔄 Comparing with QUERY1 (basic search):");
            var basicResults = await client.SearchBasicAsync(query, SearchFlags.None, CancellationToken.None);

            Console.WriteLine($"QUERY1 results: {basicResults.Length}");
            if (basicResults.Length > 0)
            {
                var firstBasic = basicResults[0];
                Console.WriteLine($"First QUERY1 result:");
                Console.WriteLine($"  Name: '{firstBasic.Name}'");
                Console.WriteLine($"  Path: '{firstBasic.Path}'");
                Console.WriteLine($"  FullPath: '{firstBasic.FullPath}'");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
}