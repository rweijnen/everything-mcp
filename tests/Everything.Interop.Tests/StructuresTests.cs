using Everything.Interop;
using FluentAssertions;
using System.Runtime.InteropServices;
using Xunit;

namespace Everything.Interop.Tests;

public class StructuresTests
{
    [Fact]
    public void CopyDataStruct_ShouldHaveCorrectLayout()
    {
        var size = Marshal.SizeOf<CopyDataStruct>();
        size.Should().BeGreaterThan(0);

        var copyData = new CopyDataStruct
        {
            dwData = (IntPtr)123,
            cbData = 456,
            lpData = (IntPtr)789
        };

        copyData.dwData.Should().Be((IntPtr)123);
        copyData.cbData.Should().Be(456u);
        copyData.lpData.Should().Be((IntPtr)789);
    }

    [Fact]
    public void EverythingIpcItemW_ShouldHaveCorrectLayout()
    {
        var size = Marshal.SizeOf<EverythingIpcItemW>();
        size.Should().Be(12); // 3 DWORDs = 12 bytes

        var item = new EverythingIpcItemW
        {
            Flags = (uint)ItemFlags.Folder,
            FilenameOffset = 100,
            PathOffset = 200
        };

        item.Flags.Should().Be((uint)ItemFlags.Folder);
        item.FilenameOffset.Should().Be(100u);
        item.PathOffset.Should().Be(200u);
    }

    [Fact]
    public void EverythingIpcItemA_ShouldHaveCorrectLayout()
    {
        var size = Marshal.SizeOf<EverythingIpcItemA>();
        size.Should().Be(12); // 3 DWORDs = 12 bytes

        var item = new EverythingIpcItemA
        {
            Flags = (uint)ItemFlags.Drive,
            FilenameOffset = 300,
            PathOffset = 400
        };

        item.Flags.Should().Be((uint)ItemFlags.Drive);
        item.FilenameOffset.Should().Be(300u);
        item.PathOffset.Should().Be(400u);
    }

    [Fact]
    public void SearchResult_ShouldSupportRecordFeatures()
    {
        var result1 = new SearchResult(
            Name: "test.txt",
            Path: @"C:\temp",
            FullPath: @"C:\temp\test.txt",
            Flags: ItemFlags.None,
            Size: 1024);

        var result2 = new SearchResult(
            Name: "test.txt",
            Path: @"C:\temp",
            FullPath: @"C:\temp\test.txt",
            Flags: ItemFlags.None,
            Size: 1024);

        var result3 = new SearchResult(
            Name: "folder",
            Path: @"C:\temp",
            FullPath: @"C:\temp\folder",
            Flags: ItemFlags.Folder);

        // Test equality
        result1.Should().Be(result2);
        result1.Should().NotBe(result3);

        // Test properties
        result1.Name.Should().Be("test.txt");
        result1.Path.Should().Be(@"C:\temp");
        result1.FullPath.Should().Be(@"C:\temp\test.txt");
        result1.Size.Should().Be(1024);
        result1.IsFile.Should().BeTrue();
        result1.IsFolder.Should().BeFalse();

        result3.IsFolder.Should().BeTrue();
        result3.IsFile.Should().BeFalse();
    }

    [Fact]
    public void SearchOptions_ShouldSupportRecordFeatures()
    {
        var options1 = new SearchOptions(
            Query: "*.txt",
            Flags: SearchFlags.MatchCase,
            Sort: SortType.NameAscending,
            MaxResults: 100);

        var options2 = new SearchOptions(
            Query: "*.txt",
            Flags: SearchFlags.MatchCase,
            Sort: SortType.NameAscending,
            MaxResults: 100);

        var options3 = new SearchOptions(
            Query: "*.doc",
            Flags: SearchFlags.MatchCase,
            Sort: SortType.NameAscending,
            MaxResults: 100);

        // Test equality
        options1.Should().Be(options2);
        options1.Should().NotBe(options3);

        // Test properties
        options1.Query.Should().Be("*.txt");
        options1.Flags.Should().Be(SearchFlags.MatchCase);
        options1.Sort.Should().Be(SortType.NameAscending);
        options1.MaxResults.Should().Be(100u);
    }

    [Fact]
    public void SearchOptions_ShouldHaveCorrectDefaults()
    {
        var options = new SearchOptions(Query: "test");

        options.Query.Should().Be("test");
        options.Flags.Should().Be(SearchFlags.None);
        options.Sort.Should().Be(SortType.NameAscending);
        options.Offset.Should().Be(0u);
        options.MaxResults.Should().Be(Constants.EVERYTHING_IPC_ALLRESULTS);
        options.RequestFlags.Should().Be(Query2RequestFlags.Name | Query2RequestFlags.Path);
    }
}