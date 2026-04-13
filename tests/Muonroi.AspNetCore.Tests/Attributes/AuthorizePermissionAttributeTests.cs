namespace Muonroi.AspNetCore.Tests.Attributes;

public class AuthorizePermissionAttributeTests
{
    [Fact]
    public void Constructor_ValidKey_SetsProperties()
    {
        var attr = new AuthorizePermissionAttribute("users.read");

        attr.PermissionKey.Should().Be("users.read");
        attr.MatchMode.Should().Be(PermissionMatchMode.All);
    }

    [Fact]
    public void Constructor_WithMatchMode_SetsMatchMode()
    {
        var attr = new AuthorizePermissionAttribute("users.read", PermissionMatchMode.Any);

        attr.PermissionKey.Should().Be("users.read");
        attr.MatchMode.Should().Be(PermissionMatchMode.Any);
    }

    [Fact]
    public void Constructor_NullKey_ThrowsInvalidPermissionException()
    {
        Action act = () => new AuthorizePermissionAttribute(null!);
        act.Should().Throw<InvalidPermissionException>();
    }

    [Fact]
    public void Constructor_EmptyKey_ThrowsInvalidPermissionException()
    {
        Action act = () => new AuthorizePermissionAttribute("");
        act.Should().Throw<InvalidPermissionException>();
    }

    [Fact]
    public void Constructor_WhitespaceKey_ThrowsInvalidPermissionException()
    {
        Action act = () => new AuthorizePermissionAttribute("   ");
        act.Should().Throw<InvalidPermissionException>();
    }

    [Fact]
    public void Constructor_InvalidMatchMode_ThrowsInvalidPermissionException()
    {
        Action act = () => new AuthorizePermissionAttribute("key", (PermissionMatchMode)999);
        act.Should().Throw<InvalidPermissionException>();
    }

    [Fact]
    public void AllowsMultipleAttribute()
    {
        var usage = typeof(AuthorizePermissionAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage.Should().NotBeNull();
        usage!.AllowMultiple.Should().BeTrue();
    }

    [Fact]
    public void TargetsClassAndMethod()
    {
        var usage = typeof(AuthorizePermissionAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage!.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.ValidOn.Should().HaveFlag(AttributeTargets.Method);
    }
}
