using Everything.Client;
using Everything.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Everything.TestConsole;

public static class BenchmarkProgram
{
    public static async Task<int> RunBenchmarkAsync(string[] args)
    {
        Console.WriteLine("⚡ Everything MCP Performance Benchmark");
        Console.WriteLine("========================================\n");

        using var loggerFactory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Warning));
        var logger = loggerFactory.CreateLogger<EverythingClient>();
        var options = Options.Create(new EverythingClientOptions());

        using var client = new EverythingClient(options, logger);

        try
        {
            // Warm-up query to initialize connection
            Console.WriteLine("Warming up connection...");
            await client.SearchAsync("*.warmup", CancellationToken.None);
            await Task.Delay(100);

            // Test queries
            var queries = new[]
            {
                "*.txt",
                "*.exe",
                "*.cs",
                "folder:Windows",
                "C:\\Program Files\\ *.dll"
            };

            var results = new List<BenchmarkResult>();

            foreach (var query in queries)
            {
                Console.WriteLine($"\nTesting query: {query}");
                Console.WriteLine("----------------------------------------");

                // Test QUERY1 (basic)
                var query1Result = await BenchmarkQuery1(client, query);
                results.Add(query1Result);

                // Test QUERY2 (with metadata)
                var query2Result = await BenchmarkQuery2(client, query);
                results.Add(query2Result);

                // Compare
                PrintComparison(query1Result, query2Result);

                await Task.Delay(100); // Brief pause between queries
            }

            // Print summary
            PrintSummary(results);

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Benchmark failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<BenchmarkResult> BenchmarkQuery1(EverythingClient client, string query)
    {
        var stopwatch = new Stopwatch();
        var runs = 5;
        var times = new List<double>();
        SearchResult[]? lastResults = null;

        for (int i = 0; i < runs; i++)
        {
            stopwatch.Restart();
            lastResults = await client.SearchBasicAsync(query, SearchFlags.None, CancellationToken.None);
            stopwatch.Stop();
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        // Calculate token estimate based on JSON response size
        var jsonResponse = JsonSerializer.Serialize(new
        {
            results = lastResults?.Take(100).Select(r => new
            {
                name = r.Name,
                path = r.Path,
                fullPath = r.FullPath
            })
        });

        var tokenEstimate = EstimateTokens(jsonResponse);

        return new BenchmarkResult
        {
            QueryType = "QUERY1 (Basic)",
            Query = query,
            AverageMs = times.Average(),
            MinMs = times.Min(),
            MaxMs = times.Max(),
            ResultCount = lastResults?.Length ?? 0,
            ResponseSizeBytes = Encoding.UTF8.GetByteCount(jsonResponse),
            EstimatedTokens = tokenEstimate,
            Times = times
        };
    }

    private static async Task<BenchmarkResult> BenchmarkQuery2(EverythingClient client, string query)
    {
        var stopwatch = new Stopwatch();
        var runs = 5;
        var times = new List<double>();
        SearchResult[]? lastResults = null;

        for (int i = 0; i < runs; i++)
        {
            stopwatch.Restart();
            lastResults = await client.SearchWithMetadataAsync(
                query,
                Query2RequestFlags.Name | Query2RequestFlags.Path | Query2RequestFlags.Size | Query2RequestFlags.DateModified,
                SearchFlags.None,
                CancellationToken.None);
            stopwatch.Stop();
            times.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        // Calculate token estimate based on JSON response size
        var jsonResponse = JsonSerializer.Serialize(new
        {
            results = lastResults?.Take(100).Select(r => new
            {
                name = r.Name,
                path = r.Path,
                fullPath = r.FullPath,
                size = r.Size,
                dateModified = r.DateModified?.ToString("yyyy-MM-ddTHH:mm:ssZ")
            })
        });

        var tokenEstimate = EstimateTokens(jsonResponse);

        return new BenchmarkResult
        {
            QueryType = "QUERY2 (Metadata)",
            Query = query,
            AverageMs = times.Average(),
            MinMs = times.Min(),
            MaxMs = times.Max(),
            ResultCount = lastResults?.Length ?? 0,
            ResponseSizeBytes = Encoding.UTF8.GetByteCount(jsonResponse),
            EstimatedTokens = tokenEstimate,
            Times = times
        };
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimation: ~4 characters per token for English text
        // JSON tends to be more token-dense due to structure
        return (int)(text.Length / 3.5);
    }

    private static void PrintComparison(BenchmarkResult query1, BenchmarkResult query2)
    {
        var speedDiff = ((query2.AverageMs - query1.AverageMs) / query1.AverageMs) * 100;
        var tokenDiff = ((query2.EstimatedTokens - query1.EstimatedTokens) / (double)query1.EstimatedTokens) * 100;

        Console.WriteLine($"  QUERY1: {query1.AverageMs:F1}ms avg, {query1.EstimatedTokens:N0} tokens");
        Console.WriteLine($"  QUERY2: {query2.AverageMs:F1}ms avg, {query2.EstimatedTokens:N0} tokens");
        Console.WriteLine($"  Speed difference: {speedDiff:+0.0;-0.0}% {(speedDiff > 0 ? "(slower)" : "(faster)")}");
        Console.WriteLine($"  Token difference: {tokenDiff:+0.0;-0.0}% {(tokenDiff > 0 ? "(more)" : "(less)")}");
        Console.WriteLine($"  Results: {query1.ResultCount:N0} items");
    }

    private static void PrintSummary(List<BenchmarkResult> results)
    {
        Console.WriteLine("\n\n📊 BENCHMARK SUMMARY");
        Console.WriteLine("===========================================");

        var query1Results = results.Where(r => r.QueryType.Contains("QUERY1")).ToList();
        var query2Results = results.Where(r => r.QueryType.Contains("QUERY2")).ToList();

        if (query1Results.Any() && query2Results.Any())
        {
            var avgSpeedQuery1 = query1Results.Average(r => r.AverageMs);
            var avgSpeedQuery2 = query2Results.Average(r => r.AverageMs);
            var avgTokensQuery1 = query1Results.Average(r => r.EstimatedTokens);
            var avgTokensQuery2 = query2Results.Average(r => r.EstimatedTokens);

            Console.WriteLine("\n🏃 Performance:");
            Console.WriteLine($"  QUERY1 Average: {avgSpeedQuery1:F1}ms");
            Console.WriteLine($"  QUERY2 Average: {avgSpeedQuery2:F1}ms");
            Console.WriteLine($"  Speed Factor: {avgSpeedQuery2 / avgSpeedQuery1:F2}x slower");

            Console.WriteLine("\n💬 Token Usage (estimated):");
            Console.WriteLine($"  QUERY1 Average: {avgTokensQuery1:N0} tokens");
            Console.WriteLine($"  QUERY2 Average: {avgTokensQuery2:N0} tokens");
            Console.WriteLine($"  Token Factor: {avgTokensQuery2 / avgTokensQuery1:F2}x more tokens");

            Console.WriteLine("\n📈 Recommendations:");
            if (avgSpeedQuery2 / avgSpeedQuery1 > 2.0)
            {
                Console.WriteLine("  ⚠️  QUERY2 is significantly slower (>2x)");
            }
            else
            {
                Console.WriteLine("  ✅ QUERY2 performance overhead is acceptable");
            }

            if (avgTokensQuery2 / avgTokensQuery1 > 1.5)
            {
                Console.WriteLine("  ⚠️  QUERY2 uses significantly more tokens (>1.5x)");
                Console.WriteLine("  💡 Use QUERY1 for basic existence checks");
                Console.WriteLine("  💡 Use QUERY2 only when metadata is needed");
            }
            else
            {
                Console.WriteLine("  ✅ Token usage difference is minimal");
            }
        }

        // Show detailed timing distribution
        Console.WriteLine("\n⏱️  Timing Distribution:");
        foreach (var result in results)
        {
            var variance = result.Times.Max() - result.Times.Min();
            Console.WriteLine($"  {result.QueryType} '{result.Query}':");
            Console.WriteLine($"    Min: {result.MinMs:F1}ms, Max: {result.MaxMs:F1}ms, Variance: {variance:F1}ms");
        }
    }

    private class BenchmarkResult
    {
        public string QueryType { get; set; } = "";
        public string Query { get; set; } = "";
        public double AverageMs { get; set; }
        public double MinMs { get; set; }
        public double MaxMs { get; set; }
        public int ResultCount { get; set; }
        public int ResponseSizeBytes { get; set; }
        public int EstimatedTokens { get; set; }
        public List<double> Times { get; set; } = new();
    }
}