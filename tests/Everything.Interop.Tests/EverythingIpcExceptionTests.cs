using Everything.Interop;
using FluentAssertions;
using Xunit;

namespace Everything.Interop.Tests;

public class EverythingIpcExceptionTests
{
    [Fact]
    public void EverythingIpcException_ShouldSupportBasicConstructors()
    {
        var ex1 = new EverythingIpcException();
        ex1.Should().NotBeNull();

        var ex2 = new EverythingIpcException("Test message");
        ex2.Message.Should().Be("Test message");

        var innerEx = new ArgumentException("Inner");
        var ex3 = new EverythingIpcException("Outer", innerEx);
        ex3.Message.Should().Be("Outer");
        ex3.InnerException.Should().Be(innerEx);
    }

    [Fact]
    public void EverythingNotRunning_ShouldCreateCorrectException()
    {
        var ex = EverythingIpcException.EverythingNotRunning();

        ex.Should().NotBeNull();
        ex.Message.Should().Contain("Everything is not running");
        ex.Message.Should().Contain("Please start Everything and try again");
    }

    [Fact]
    public void WindowNotFound_ShouldCreateCorrectException()
    {
        var ex = EverythingIpcException.WindowNotFound();

        ex.Should().NotBeNull();
        ex.Message.Should().Contain("Could not find Everything window");
        ex.Message.Should().Contain("Ensure Everything is running");
    }

    [Fact]
    public void SendMessageFailed_ShouldIncludeOperation()
    {
        var operation = "TestOperation";
        var ex = EverythingIpcException.SendMessageFailed(operation);

        ex.Should().NotBeNull();
        ex.Message.Should().Contain("Failed to send message to Everything");
        ex.Message.Should().Contain(operation);
    }

    [Fact]
    public void InvalidResponse_ShouldIncludeOperation()
    {
        var operation = "GetVersion";
        var ex = EverythingIpcException.InvalidResponse(operation);

        ex.Should().NotBeNull();
        ex.Message.Should().Contain("Received invalid response from Everything");
        ex.Message.Should().Contain(operation);
    }

    [Fact]
    public void TimeoutError_ShouldIncludeOperation()
    {
        var operation = "Search";
        var ex = EverythingIpcException.TimeoutError(operation);

        ex.Should().NotBeNull();
        ex.Message.Should().Contain("Timeout occurred");
        ex.Message.Should().Contain(operation);
    }

    [Fact]
    public void MemoryAllocationFailed_ShouldCreateCorrectException()
    {
        var ex = EverythingIpcException.MemoryAllocationFailed();

        ex.Should().NotBeNull();
        ex.Message.Should().Contain("Failed to allocate memory");
        ex.Message.Should().Contain("IPC communication");
    }

    [Fact]
    public void QueryTooLarge_ShouldIncludeMaxSize()
    {
        var maxSize = 32000;
        var ex = EverythingIpcException.QueryTooLarge(maxSize);

        ex.Should().NotBeNull();
        ex.Message.Should().Contain("Query is too large");
        ex.Message.Should().Contain(maxSize.ToString());
    }
}