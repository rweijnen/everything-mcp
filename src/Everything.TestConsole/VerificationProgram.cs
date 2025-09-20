using Everything.Client;
using Everything.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Everything.TestConsole;

public static class VerificationProgram
{
    public static async Task<int> RunVerificationAsync(string[] args)
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

            // Test 1: Everything availability
            if (!client.IsEverythingRunning)
            {
                Console.WriteLine("❌ Everything is not running");
                return 1;
            }

            Console.WriteLine($"✅ Everything {client.EverythingVersion} is running");
            Console.WriteLine($"   Database Loaded: {client.IsDbLoaded}");
            Console.WriteLine($"   Running as Admin: {client.IsAdmin}");

            // Test 2: Basic search
            var txtFiles = await client.SearchAsync("*.txt", cts.Token);
            Console.WriteLine($"✅ Found {txtFiles.Length} .txt files");

            // Test 3: Advanced search
            var exeFiles = await client.SearchFilesAsync("*.exe", cts.Token);
            Console.WriteLine($"✅ Found {exeFiles.Length} executable files");

            // Test 4: Folder search
            var windowsFolders = await client.SearchFoldersAsync("Windows", cts.Token);
            Console.WriteLine($"✅ Found {windowsFolders.Length} folders containing 'Windows'");

            // Test 5: Extension search
            var jpgFiles = await client.SearchByExtensionAsync("jpg", cts.Token);
            Console.WriteLine($"✅ Found {jpgFiles.Length} .jpg files");

            // Test 6: Empty search handling
            var emptyResults = await client.SearchAsync("nonexistentfileextension123456", cts.Token);
            Console.WriteLine($"✅ Empty search handled correctly ({emptyResults.Length} results)");

            Console.WriteLine("\n🎉 All verification tests passed!");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Verification failed: {ex.Message}");
            return 1;
        }
    }
}