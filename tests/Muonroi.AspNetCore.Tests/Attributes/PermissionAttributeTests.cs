namespace Muonroi.AspNetCore.Tests.Attributes;

public enum TestPermission
{
    Read = 0,
    Write = 1,
    Admin = 2
}

public class PermissionAttributeGenericTests
{
    [Fact]
    public void Constructor_ValidPermission_SetsProperties()
    {
        var attr = new PermissionAttribute<TestPermission>(TestPermission.Read);

        attr.RequiredPermission.Should().Be(TestPermission.Read);
        attr.MatchMode.Should().Be(PermissionMatchMode.Any);
    }

    [Fact]
    public void Constructor_WithMatchMode_SetsMatchMode()
    {
        var attr = new PermissionAttribute<TestPermission>(TestPermission.Write, PermissionMatchMode.All);

        attr.RequiredPermission.Should().Be(TestPermission.Write);
        attr.MatchMode.Should().Be(PermissionMatchMode.All);
    }

    [Fact]
    public void Constructor_InvalidPermission_ThrowsInvalidPermissionException()
    {
        Action act = () => new PermissionAttribute<TestPermission>((TestPermission)999);
        act.Should().Throw<InvalidPermissionException>();
    }

    [Fact]
    public void Constructor_InvalidMatchMode_ThrowsInvalidPermissionException()
    {
        Action act = () => new PermissionAttribute<TestPermission>(TestPermission.Read, (PermissionMatchMode)999);
        act.Should().Throw<InvalidPermissionException>();
    }

    [Fact]
    public void AllowsMultipleAttribute()
    {
        var usage = typeof(PermissionAttribute<TestPermission>)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage!.AllowMultiple.Should().BeTrue();
    }
}
