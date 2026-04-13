namespace Muonroi.BuildingBlock.Test;

public class MMongoDbEntityTests
{
    private sealed class TestMongoEntity : MMongoDbEntity
    {
        public TestMongoEntity(string? id = null)
        {
            Id = id;
        }

        public void SetCreatedDate(DateTime dt)
        {
            CreatedDate = dt;
        }
    }

    private class TestEntity : MMongoDbEntity
    {
    }

    [Fact]
    public void Id_Returns_Value_When_Set()
    {
        TestMongoEntity e = new("abc");
        Assert.Equal("abc", e.Id);
    }

    [Fact]
    public void Id_Null_When_Not_Set()
    {
        TestMongoEntity e = new();
        Assert.Null(e.Id);
    }

    [Fact]
    public void CreatedDate_Default_Is_Now()
    {
        TestMongoEntity e = new();
        Assert.True((DateTime.UtcNow - e.CreatedDate).TotalSeconds < 5);
    }

    [Fact]
    public void CreatedDate_Can_Be_Set()
    {
        TestMongoEntity e = new();
        DateTime dt = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        e.SetCreatedDate(dt);
        Assert.Equal(dt, e.CreatedDate);
    }

    [Fact]
    public void LastModifiedDate_Get_Returns_Value()
    {
        TestEntity entity = new()
        {
            LastModifiedDate = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        };
        Assert.Equal(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc), entity.LastModifiedDate);

        TestEntity defaultEntity = new();
        Assert.True(defaultEntity.LastModifiedDate <= DateTime.UtcNow);
    }
}
