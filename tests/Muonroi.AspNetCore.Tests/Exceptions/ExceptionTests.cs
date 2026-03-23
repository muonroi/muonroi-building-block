namespace Muonroi.AspNetCore.Tests.Exceptions;

public class InvalidPermissionExceptionTests
{
    [Fact]
    public void Constructor_SetsMessage()
    {
        var ex = new InvalidPermissionException("test message");
        ex.Message.Should().Be("test message");
    }

    [Fact]
    public void Constructor_NullMessage_DoesNotThrow()
    {
        var ex = new InvalidPermissionException(null);
        ex.Should().NotBeNull();
    }

    [Fact]
    public void IsException()
    {
        var ex = new InvalidPermissionException("msg");
        ex.Should().BeAssignableTo<Exception>();
    }
}

public class PermissionDeniedExceptionTests
{
    [Fact]
    public void Constructor_SetsMessage()
    {
        var ex = new PermissionDeniedException("denied");
        ex.Message.Should().Be("denied");
    }

    [Fact]
    public void Constructor_NullMessage_DoesNotThrow()
    {
        var ex = new PermissionDeniedException(null);
        ex.Should().NotBeNull();
    }

    [Fact]
    public void IsException()
    {
        var ex = new PermissionDeniedException("msg");
        ex.Should().BeAssignableTo<Exception>();
    }
}
