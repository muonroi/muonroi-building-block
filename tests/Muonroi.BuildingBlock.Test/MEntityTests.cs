namespace Muonroi.BuildingBlock.Test;

public class MEntityTests
{
    private class DummyEvent : INotification { }

    [Fact]
    public void RemoveDomainEvent_Removes_Item()
    {
        MUser entity = new();
        DummyEvent ev = new();
        entity.AddDomainEvent(ev);
        entity.RemoveDomainEvent(ev);
        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public void RemoveDomainEvent_NotExists_Does_Nothing()
    {
        MUser entity = new();
        DummyEvent ev1 = new();
        DummyEvent ev2 = new();
        entity.AddDomainEvent(ev1);
        entity.RemoveDomainEvent(ev2);
        Assert.Single(entity.DomainEvents);
        Assert.Contains(ev1, entity.DomainEvents);
    }

    [Fact]
    public void RemoveDomainEvent_Null_Does_Nothing()
    {
        MUser entity = new();
        DummyEvent ev = new();
        entity.AddDomainEvent(ev);
        entity.RemoveDomainEvent(null!);
        Assert.Single(entity.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_Removes_All()
    {
        MUser entity = new();
        entity.AddDomainEvent(new DummyEvent());
        entity.AddDomainEvent(new DummyEvent());
        entity.ClearDomainEvents();
        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_NoEvents_DoesNothing()
    {
        MUser entity = new();
        entity.ClearDomainEvents();
        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public void IsTransient_Returns_Expected()
    {
        MUser entity = new();
        Assert.True(entity.IsTransient());
        entity.Id = 1;
        Assert.False(entity.IsTransient());
    }

    [Fact]
    public void Equals_Compares_By_EntityId()
    {
        MUser e1 = new()
        {
            Id = 1
        };
        MUser e2 = new()
        {
            Id = 2,
            EntityId = e1.EntityId
        };
        MUser e3 = new()
        {
            Id = 3
        };
        Assert.True(e1.Equals(e2));
        Assert.False(e1.Equals(e3));
        Assert.False(e1.Equals(null));
    }

    [Fact]
    public void GetHashCode_Same_Id_Same_Hash()
    {
        MUser e1 = new()
        {
            Id = 1
        };
        MUser e2 = new()
        {
            Id = 2,
            EntityId = e1.EntityId
        };
        Assert.Equal(e1.GetHashCode(), e2.GetHashCode());
    }
}
