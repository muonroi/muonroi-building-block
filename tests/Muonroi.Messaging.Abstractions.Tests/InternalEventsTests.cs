namespace Muonroi.Messaging.Abstractions.Tests;

using Muonroi.Messaging.Abstractions.Events;
using Muonroi.Core.Abstractions.SeedWorks;

public class InternalEventsTests
{
    private sealed class TestEntity : MEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void MEntityCreatedEvent_Should_Store_Entity()
    {
        TestEntity entity = new() { Name = "test" };

        MEntityCreatedEvent<TestEntity> evt = new(entity);

        evt.Data.Should().BeSameAs(entity);
        evt.Data.Name.Should().Be("test");
    }

    [Fact]
    public void MEntityChangedEvent_Should_Store_Entity()
    {
        TestEntity entity = new() { Name = "changed" };

        MEntityChangedEvent<TestEntity> evt = new(entity);

        evt.Data.Should().BeSameAs(entity);
    }

    [Fact]
    public void MEntityDeletedEvent_Should_Store_Entity()
    {
        TestEntity entity = new() { Name = "deleted" };

        MEntityDeletedEvent<TestEntity> evt = new(entity);

        evt.Data.Should().BeSameAs(entity);
    }

    [Fact]
    public void MEntitiesCreatedEvent_Should_Store_Collection()
    {
        List<TestEntity> entities = [new() { Name = "a" }, new() { Name = "b" }];

        MEntitiesCreatedEvent<TestEntity> evt = new(entities);

        evt.Data.Should().HaveCount(2);
    }

    [Fact]
    public void MEntitiesChangedEvent_Should_Store_Collection()
    {
        List<TestEntity> entities = [new() { Name = "x" }];

        MEntitiesChangedEvent<TestEntity> evt = new(entities);

        evt.Data.Should().HaveCount(1);
    }

    [Fact]
    public void MEntitiesDeletedEvent_Should_Store_Collection()
    {
        List<TestEntity> entities = [new() { Name = "del1" }, new() { Name = "del2" }, new() { Name = "del3" }];

        MEntitiesDeletedEvent<TestEntity> evt = new(entities);

        evt.Data.Should().HaveCount(3);
    }

    [Fact]
    public void Event_Data_Should_Be_Mutable()
    {
        TestEntity original = new() { Name = "orig" };
        TestEntity replacement = new() { Name = "replaced" };

        MEntityCreatedEvent<TestEntity> evt = new(original);
        evt.Data = replacement;

        evt.Data.Should().BeSameAs(replacement);
    }
}
