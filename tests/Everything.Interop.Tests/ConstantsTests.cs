using Everything.Interop;
using FluentAssertions;
using Xunit;

namespace Everything.Interop.Tests;

public class ConstantsTests
{
    [Fact]
    public void Constants_ShouldHaveCorrectValues()
    {
        Constants.EVERYTHING_WM_IPC.Should().Be(0x0400u);
        Constants.WM_COPYDATA.Should().Be(0x004Au);
        Constants.EVERYTHING_IPC_WNDCLASS.Should().Be("EVERYTHING_TASKBAR_NOTIFICATION");
        Constants.EVERYTHING_IPC_SEARCH_CLIENT_WNDCLASS.Should().Be("EVERYTHING");
        Constants.EVERYTHING_IPC_ALLRESULTS.Should().Be(0xFFFFFFFFu);
    }

    [Fact]
    public void EverythingIpcCommands_ShouldHaveCorrectValues()
    {
        EverythingIpcCommands.GET_MAJOR_VERSION.Should().Be(0u);
        EverythingIpcCommands.GET_MINOR_VERSION.Should().Be(1u);
        EverythingIpcCommands.GET_REVISION.Should().Be(2u);
        EverythingIpcCommands.GET_BUILD_NUMBER.Should().Be(3u);
        EverythingIpcCommands.EXIT.Should().Be(4u);
        EverythingIpcCommands.GET_TARGET_MACHINE.Should().Be(5u);
    }

    [Fact]
    public void TargetMachine_ShouldHaveCorrectValues()
    {
        ((uint)TargetMachine.X86).Should().Be(1u);
        ((uint)TargetMachine.X64).Should().Be(2u);
        ((uint)TargetMachine.ARM).Should().Be(3u);
        ((uint)TargetMachine.ARM64).Should().Be(4u);
    }

    [Fact]
    public void BuiltInFilter_ShouldHaveCorrectValues()
    {
        ((uint)BuiltInFilter.Everything).Should().Be(0u);
        ((uint)BuiltInFilter.Audio).Should().Be(1u);
        ((uint)BuiltInFilter.Compressed).Should().Be(2u);
        ((uint)BuiltInFilter.Document).Should().Be(3u);
        ((uint)BuiltInFilter.Executable).Should().Be(4u);
        ((uint)BuiltInFilter.Folder).Should().Be(5u);
        ((uint)BuiltInFilter.Picture).Should().Be(6u);
        ((uint)BuiltInFilter.Video).Should().Be(7u);
        ((uint)BuiltInFilter.Custom).Should().Be(8u);
    }

    [Theory]
    [InlineData(SearchFlags.MatchCase, 0x00000001u)]
    [InlineData(SearchFlags.MatchWholeWord, 0x00000002u)]
    [InlineData(SearchFlags.MatchPath, 0x00000004u)]
    [InlineData(SearchFlags.Regex, 0x00000008u)]
    [InlineData(SearchFlags.MatchDiacritics, 0x00000010u)]
    [InlineData(SearchFlags.MatchPrefix, 0x00000020u)]
    [InlineData(SearchFlags.MatchSuffix, 0x00000040u)]
    [InlineData(SearchFlags.IgnorePunctuation, 0x00000080u)]
    [InlineData(SearchFlags.IgnoreWhitespace, 0x00000100u)]
    public void SearchFlags_ShouldHaveCorrectValues(SearchFlags flag, uint expectedValue)
    {
        ((uint)flag).Should().Be(expectedValue);
    }

    [Fact]
    public void SearchFlags_ShouldSupportCombination()
    {
        var combined = SearchFlags.MatchCase | SearchFlags.MatchWholeWord | SearchFlags.Regex;
        ((uint)combined).Should().Be(0x00000001u | 0x00000002u | 0x00000008u);
    }

    [Theory]
    [InlineData(ItemFlags.Folder, 0x00000001u)]
    [InlineData(ItemFlags.Drive, 0x00000002u)]
    public void ItemFlags_ShouldHaveCorrectValues(ItemFlags flag, uint expectedValue)
    {
        ((uint)flag).Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(SortType.NameAscending, 1u)]
    [InlineData(SortType.NameDescending, 2u)]
    [InlineData(SortType.PathAscending, 3u)]
    [InlineData(SortType.PathDescending, 4u)]
    [InlineData(SortType.SizeAscending, 5u)]
    [InlineData(SortType.SizeDescending, 6u)]
    public void SortType_ShouldHaveCorrectValues(SortType sort, uint expectedValue)
    {
        ((uint)sort).Should().Be(expectedValue);
    }
}