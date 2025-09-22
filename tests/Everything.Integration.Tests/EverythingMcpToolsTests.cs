using Xunit;
using Xunit.Abstractions;

namespace Everything.Integration.Tests;

/// <summary>
/// Integration tests for EverythingMcpTools using a shared fixture
/// to mimic real MCP server behavior (single instance handling all requests)
/// </summary>
public class EverythingMcpToolsTests : IClassFixture<SharedMcpFixture>
{
    private readonly SharedMcpFixture _fixture;
    private readonly ITestOutputHelper _output;

    public EverythingMcpToolsTests(SharedMcpFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task search_files_SystemScope_ShouldReturnResults()
    {
        // Arrange
        var query = "*.txt";
        var scope = "system";
        var includeMetadata = false;
        var maxResults = 5;

        // Act
        var result = await _fixture.MpcTools.search_files(query, scope, includeMetadata, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"search_files system scope result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task search_files_CurrentDirectoryScope_ShouldSearchCurrentOnly()
    {
        // Arrange
        var query = "*.cs";
        var scope = "current";
        var includeMetadata = false;
        var maxResults = 5;

        // Act
        var result = await _fixture.MpcTools.search_files(query, scope, includeMetadata, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"search_files current directory scope result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task search_files_RecursiveScope_ShouldSearchCurrentAndSubdirectories()
    {
        // Arrange
        var query = "*.cs";
        var scope = "recursive";
        var includeMetadata = false;
        var maxResults = 10;

        // Act
        var result = await _fixture.MpcTools.search_files(query, scope, includeMetadata, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"search_files recursive scope result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task search_files_CustomPathScope_ShouldSearchSpecifiedPath()
    {
        // Arrange
        var query = "*.cs";
        var scope = @"path:C:\Users\me\source\repos\rweijnen\everything-mcp\src";
        var includeMetadata = false;
        var maxResults = 10;

        // Act
        var result = await _fixture.MpcTools.search_files(query, scope, includeMetadata, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"search_files custom path scope result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task search_files_WithMetadata_ShouldReturnMetadata()
    {
        // Arrange
        var query = "*.exe";
        var scope = "system";
        var includeMetadata = true;
        var maxResults = 3;

        // Act
        var result = await _fixture.MpcTools.search_files(query, scope, includeMetadata, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"search_files with metadata result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task search_files_DefaultScope_ShouldUseCurrent()
    {
        // Arrange
        var query = "*.json";
        // Note: scope parameter has default value of "current"

        // Act - using default scope
        var result = await _fixture.MpcTools.search_files(query);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"search_files default scope result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task search_in_project_ShouldReturnProjectFiles()
    {
        // Arrange
        var projectPath = @"C:\Users\me\source\repos\rweijnen\everything-mcp";
        var pattern = "*.cs";
        var includeMetadata = true;
        var maxResults = 10;

        // Act
        var result = await _fixture.MpcTools.search_in_project(projectPath, pattern, includeMetadata, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"search_in_project result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task find_executable_ExactMatch_ShouldReturnSpecificExecutable()
    {
        // Arrange
        var name = "notepad.exe";
        var exactMatch = true;
        var maxResults = 5;

        // Act
        var result = await _fixture.MpcTools.find_executable(name, exactMatch, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"find_executable exact result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task find_executable_BroadSearch_ShouldReturnMultipleFormats()
    {
        // Arrange
        var name = "notepad";
        var exactMatch = false;
        var maxResults = 5;

        // Act
        var result = await _fixture.MpcTools.find_executable(name, exactMatch, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"find_executable broad result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task find_source_files_ShouldReturnSourceCode()
    {
        // Arrange
        var filename = "Program";
        var extensions = "cs,js,ts";
        var includeMetadata = false;
        var maxResults = 5;

        // Act
        var result = await _fixture.MpcTools.find_source_files(filename, extensions, includeMetadata, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"find_source_files result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task search_recent_files_ShouldReturnRecentFiles()
    {
        // Arrange
        var hours = 24;
        var pattern = "*.txt";
        var includeMetadata = true;
        var maxResults = 10;

        // Act
        var result = await _fixture.MpcTools.search_recent_files(hours, pattern, includeMetadata, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"search_recent_files result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task find_config_files_InProject_ShouldReturnConfigFiles()
    {
        // Arrange
        var projectPath = @"C:\Users\me\source\repos\rweijnen\everything-mcp";
        var includeMetadata = false;
        var maxResults = 20;

        // Act
        var result = await _fixture.MpcTools.find_config_files(projectPath, includeMetadata, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"find_config_files result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

    [Fact]
    public async Task find_config_files_Global_ShouldReturnSystemConfigs()
    {
        // Arrange
        string? projectPath = null; // Global search
        var includeMetadata = false;
        var maxResults = 5;

        // Act
        var result = await _fixture.MpcTools.find_config_files(projectPath, includeMetadata, maxResults);

        // Assert
        Assert.NotNull(result);

        _output.WriteLine($"find_config_files global result: {System.Text.Json.JsonSerializer.Serialize(result)}");
    }

}