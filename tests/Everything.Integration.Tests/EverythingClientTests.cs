using Everything.Client;
using Everything.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Everything.Integration.Tests;

public class EverythingClientTests : IDisposable
{
    private readonly EverythingClient _client;
    private readonly ITestOutputHelper _output;

    public EverythingClientTests(ITestOutputHelper output)
    {
        _output = output;

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<EverythingClient>();
        var options = Options.Create(new EverythingClientOptions());

        _client = new EverythingClient(options, logger);
    }

    [Fact]
    public void IsEverythingRunning_ShouldReturnTrue()
    {
        // Arrange & Act
        var isRunning = _client.IsEverythingRunning;

        // Assert
        Assert.True(isRunning, "Everything should be running for integration tests");
    }

    [Fact]
    public async Task SearchBasicAsync_ShouldReturnResults()
    {
        // Arrange
        var query = "*.txt";

        // Act
        var results = await _client.SearchBasicAsync(query);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, result =>
        {
            Assert.False(string.IsNullOrEmpty(result.Name));
            Assert.False(string.IsNullOrEmpty(result.Path));
            Assert.False(string.IsNullOrEmpty(result.FullPath));
            Assert.EndsWith(".txt", result.Name, StringComparison.OrdinalIgnoreCase);
        });

        _output.WriteLine($"QUERY1 returned {results.Length} results");
        _output.WriteLine($"First result: {results[0].FullPath}");
    }

    [Fact]
    public async Task SearchWithMetadataAsync_ShouldReturnResultsWithMetadata()
    {
        // Arrange
        var query = "*.txt";
        var requestFlags = Query2RequestFlags.Name | Query2RequestFlags.Path |
                          Query2RequestFlags.Size | Query2RequestFlags.DateModified;

        // Act
        var results = await _client.SearchWithMetadataAsync(query, requestFlags);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, result =>
        {
            Assert.False(string.IsNullOrEmpty(result.Name));
            Assert.False(string.IsNullOrEmpty(result.Path));
            Assert.False(string.IsNullOrEmpty(result.FullPath));
            Assert.EndsWith(".txt", result.Name, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(result.Size);
            Assert.True(result.Size >= 0);
            Assert.NotNull(result.DateModified);
        });

        _output.WriteLine($"QUERY2 returned {results.Length} results");
        _output.WriteLine($"First result: {results[0].FullPath} ({results[0].Size} bytes, modified: {results[0].DateModified})");
    }

    [Fact]
    public async Task SearchWithMetadataAsync_ShouldMatchBasicSearch()
    {
        // Arrange
        var query = "*.txt";

        // Act
        var basicResults = await _client.SearchBasicAsync(query);
        var metadataResults = await _client.SearchWithMetadataAsync(query,
            Query2RequestFlags.Name | Query2RequestFlags.Path);

        // Assert
        Assert.Equal(basicResults.Length, metadataResults.Length);

        // Compare first few results to ensure they match
        var compareCount = Math.Min(5, basicResults.Length);
        for (int i = 0; i < compareCount; i++)
        {
            Assert.Equal(basicResults[i].Name, metadataResults[i].Name);
            Assert.Equal(basicResults[i].Path, metadataResults[i].Path);
            Assert.Equal(basicResults[i].FullPath, metadataResults[i].FullPath);
        }

        _output.WriteLine($"Both QUERY1 and QUERY2 returned {basicResults.Length} results");
    }

    [Fact]
    public async Task SearchFilesAsync_ShouldReturnOnlyFiles()
    {
        // Arrange
        var query = "*.exe";

        // Act
        var results = await _client.SearchFilesAsync(query);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, result =>
        {
            Assert.False(result.IsFolder);
            Assert.EndsWith(".exe", result.Name, StringComparison.OrdinalIgnoreCase);
        });

        _output.WriteLine($"Found {results.Length} executable files");
    }

    [Fact]
    public async Task SearchFoldersAsync_ShouldReturnOnlyFolders()
    {
        // Arrange
        var query = "Windows";

        // Act
        var results = await _client.SearchFoldersAsync(query);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, result =>
        {
            Assert.True(result.IsFolder);
            Assert.Contains("Windows", result.Name, StringComparison.OrdinalIgnoreCase);
        });

        _output.WriteLine($"Found {results.Length} folders containing 'Windows'");
    }

    [Fact]
    public async Task SearchByExtensionAsync_ShouldReturnCorrectExtension()
    {
        // Arrange
        var extension = "txt";

        // Act
        var results = await _client.SearchByExtensionAsync(extension);

        // Assert
        Assert.NotEmpty(results);
        Assert.All(results, result =>
        {
            Assert.EndsWith($".{extension}", result.Name, StringComparison.OrdinalIgnoreCase);
        });

        _output.WriteLine($"Found {results.Length} .{extension} files");
    }

    [Theory]
    [InlineData("*.txt", SearchFlags.None)]
    [InlineData("README", SearchFlags.MatchCase)]
    [InlineData("*.exe", SearchFlags.MatchPath)]
    public async Task SearchAsync_WithFlags_ShouldRespectFlags(string query, SearchFlags flags)
    {
        // Act
        var results = await _client.SearchAsync(query, flags);

        // Assert
        Assert.NotNull(results);
        // Results should be non-empty for common patterns
        if (query == "*.txt" || query == "*.exe")
        {
            Assert.NotEmpty(results);
        }

        _output.WriteLine($"Query '{query}' with flags {flags} returned {results.Length} results");
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}