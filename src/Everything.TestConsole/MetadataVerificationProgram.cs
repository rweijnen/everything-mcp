using Everything.Client;
using Everything.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Everything.TestConsole;

public static class MetadataVerificationProgram
{
    public static async Task<int> RunMetadataVerificationAsync(string[] args)
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

            Console.WriteLine($"📊 Everything Metadata Verification");
            Console.WriteLine($"   Version: {client.EverythingVersion}");
            Console.WriteLine();

            // Get a few specific files for metadata analysis
            var txtFiles = await client.SearchAsync("*.txt", cts.Token);
            Console.WriteLine($"📄 Found {txtFiles.Length} .txt files");

            if (txtFiles.Length > 0)
            {
                Console.WriteLine("   Analyzing first few results for metadata:");

                var samplesToShow = Math.Min(3, txtFiles.Length);
                for (int i = 0; i < samplesToShow; i++)
                {
                    var file = txtFiles[i];
                    Console.WriteLine($"   📄 {file.Name}");
                    Console.WriteLine($"      Path: {file.Path}");
                    Console.WriteLine($"      Flags: {file.Flags}");
                    Console.WriteLine($"      Size: {file.Size?.ToString("N0") ?? "Not available"}");
                    Console.WriteLine($"      Created: {file.DateCreated?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not available"}");
                    Console.WriteLine($"      Modified: {file.DateModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not available"}");
                    Console.WriteLine($"      Accessed: {file.DateAccessed?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not available"}");
                    Console.WriteLine($"      Attributes: {file.Attributes?.ToString("X") ?? "Not available"}");
                    Console.WriteLine($"      Run Count: {file.RunCount?.ToString() ?? "Not available"}");
                    Console.WriteLine($"      Date Run: {file.DateRun?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Not available"}");
                    Console.WriteLine();
                }
            }

            // Test with larger files to see if they have metadata
            var largeFiles = await client.SearchAsync("size:>1MB", cts.Token);
            Console.WriteLine($"💾 Found {largeFiles.Length} files larger than 1MB");

            if (largeFiles.Length > 0)
            {
                var file = largeFiles[0];
                Console.WriteLine($"   First large file: {file.Name}");
                Console.WriteLine($"   Size available: {(file.Size.HasValue ? "Yes" : "No")}");
                Console.WriteLine($"   Date fields available: {(file.DateModified.HasValue ? "Yes" : "No")}");
            }

            if (txtFiles.All(f => !f.Size.HasValue))
            {
                Console.WriteLine("⚠️  No size metadata found - need to implement QUERY2 for full metadata");
                return 1;
            }
            else
            {
                Console.WriteLine("✅ Metadata fields are available");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Metadata verification failed: {ex.Message}");
            return 1;
        }
    }
}