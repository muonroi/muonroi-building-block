namespace Muonroi.AspNetCore.Tests.Models;

public class CrudContextTests
{
    private sealed class TestEntity : MEntity { }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var context = new CrudContext<TestEntity>();

        context.Entity.Should().BeNull();
        context.OriginalEntity.Should().BeNull();
        context.OperationType.Should().Be(CrudOperationType.Create);
        context.UserId.Should().BeNull();
        context.TenantId.Should().BeNull();
        context.ValidationErrors.Should().BeEmpty();
        context.Metadata.Should().BeEmpty();
        context.CancelOperation.Should().BeFalse();
        context.CancellationReason.Should().BeNull();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var entity = new TestEntity();
        var context = new CrudContext<TestEntity>
        {
            Entity = entity,
            OriginalEntity = entity,
            OperationType = CrudOperationType.Update,
            UserId = Guid.NewGuid(),
            TenantId = "tenant-1",
            CancelOperation = true,
            CancellationReason = "Business rule violation"
        };

        context.Entity.Should().BeSameAs(entity);
        context.OriginalEntity.Should().BeSameAs(entity);
        context.OperationType.Should().Be(CrudOperationType.Update);
        context.UserId.Should().NotBeNull();
        context.TenantId.Should().Be("tenant-1");
        context.CancelOperation.Should().BeTrue();
        context.CancellationReason.Should().Be("Business rule violation");
    }

    [Fact]
    public void ValidationErrors_CanBeAdded()
    {
        var context = new CrudContext<TestEntity>();

        context.ValidationErrors.Add("Field is required");
        context.ValidationErrors.Add("Value out of range");

        context.ValidationErrors.Should().HaveCount(2);
    }

    [Fact]
    public void Metadata_CanBeAdded()
    {
        var context = new CrudContext<TestEntity>();

        context.Metadata["key1"] = "value1";
        context.Metadata["key2"] = 42;

        context.Metadata.Should().HaveCount(2);
        context.Metadata["key1"].Should().Be("value1");
    }
}

public class CrudOperationTypeTests
{
    [Theory]
    [InlineData(CrudOperationType.Create)]
    [InlineData(CrudOperationType.Update)]
    [InlineData(CrudOperationType.Delete)]
    [InlineData(CrudOperationType.Read)]
    public void AllOperationTypes_AreDefined(CrudOperationType type)
    {
        Enum.IsDefined(typeof(CrudOperationType), type).Should().BeTrue();
    }
}
