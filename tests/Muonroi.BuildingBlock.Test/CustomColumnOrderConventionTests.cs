using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Data.EntityFrameworkCore.Entity.EFConfig;
using Xunit;

namespace Muonroi.BuildingBlock.Test;

public class CustomColumnOrderConventionTests
{
    private class DummyEntity : MEntity
    {
        public string Extra1 { get; set; } = string.Empty;
        public int Extra2 { get; set; }
    }

    private class DummyContext(DbContextOptions<DummyContext> options) : DbContext(options)
    {
        public DbSet<DummyEntity> Entities => Set<DummyEntity>();
    }

    [Fact]
    public void Customize_WithColumns_AssignsSequentialOrder()
    {
        ModelBuilder builder = new(new ConventionSet());
        builder.Entity<DummyEntity>();

        DbContextOptions<DummyContext> options = new DbContextOptionsBuilder<DummyContext>()
            .UseInMemoryDatabase("order").Options;
        using DummyContext ctx = new(options);

        CustomColumnOrderConvention conv = new();
        conv.Customize(builder, ctx);

        Microsoft.EntityFrameworkCore.Metadata.IMutableEntityType entity = builder.Model.FindEntityType(typeof(DummyEntity))!;
        List<int> orders = [.. entity.GetProperties()
            .Select(p => p.GetColumnOrder() ?? -1)
            .OrderBy(o => o)];

        Assert.Equal(Enumerable.Range(0, orders.Count), orders);
    }

    [Fact]
    public void Customize_NoEntities_NoError()
    {
        ModelBuilder builder = new(new ConventionSet());
        DbContextOptions<DummyContext> options = new DbContextOptionsBuilder<DummyContext>()
            .UseInMemoryDatabase("empty").Options;
        using DummyContext ctx = new(options);
        CustomColumnOrderConvention conv = new();

        Exception ex = Record.Exception(() => conv.Customize(builder, ctx));
        Assert.Null(ex);
        Assert.Empty(builder.Model.GetEntityTypes());
    }
}
