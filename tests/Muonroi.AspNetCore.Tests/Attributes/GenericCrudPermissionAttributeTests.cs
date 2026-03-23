namespace Muonroi.AspNetCore.Tests.Attributes;

public class GenericCrudPermissionAttributeTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var attr = new GenericCrudPermissionAttribute();

        attr.ReadPermission.Should().BeNull();
        attr.CreatePermission.Should().BeNull();
        attr.UpdatePermission.Should().BeNull();
        attr.DeletePermission.Should().BeNull();
        attr.PermissionPrefix.Should().BeNull();
        attr.SkipPermissionCheck.Should().BeFalse();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var attr = new GenericCrudPermissionAttribute
        {
            ReadPermission = "Product.View",
            CreatePermission = "Product.Create",
            UpdatePermission = "Product.Update",
            DeletePermission = "Product.Delete",
            PermissionPrefix = "Products",
            SkipPermissionCheck = true
        };

        attr.ReadPermission.Should().Be("Product.View");
        attr.CreatePermission.Should().Be("Product.Create");
        attr.UpdatePermission.Should().Be("Product.Update");
        attr.DeletePermission.Should().Be("Product.Delete");
        attr.PermissionPrefix.Should().Be("Products");
        attr.SkipPermissionCheck.Should().BeTrue();
    }

    [Fact]
    public void DoesNotAllowMultiple()
    {
        var usage = typeof(GenericCrudPermissionAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage!.AllowMultiple.Should().BeFalse();
    }

    [Fact]
    public void TargetsClassOnly()
    {
        var usage = typeof(GenericCrudPermissionAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage!.ValidOn.Should().Be(AttributeTargets.Class);
    }

    [Fact]
    public void IsInherited()
    {
        var usage = typeof(GenericCrudPermissionAttribute)
            .GetCustomAttribute<AttributeUsageAttribute>();

        usage!.Inherited.Should().BeTrue();
    }
}
