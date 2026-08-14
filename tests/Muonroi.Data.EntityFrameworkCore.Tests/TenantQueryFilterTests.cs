namespace Muonroi.Data.EntityFrameworkCore.Tests;

/// <summary>
/// Tests for tenant query filter and creator filter application in MDbContext.OnModelCreating.
/// </summary>
public class TenantQueryFilterTests
{
    private sealed class FilterTestDbContext(DbContextOptions<FilterTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
        public DbSet<OwnedEntity> OwnedEntities => Set<OwnedEntity>();
    }

    [Table("OwnedEntities")]
    private sealed class OwnedEntity : MEntity
    {
        [Required]
        [StringLength(64)]
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public void OnModelCreating_Skips_CreatorFilter_For_Identity_System_Types()
    {
        DbContextOptions<FilterTestDbContext> options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using FilterTestDbContext db = new(options);
        IModel model = db.Model;

        IEntityType? userType = model.FindEntityType(typeof(MUser));
        userType.Should().NotBeNull();

        System.Linq.Expressions.LambdaExpression? filter = userType!.GetQueryFilter();
        filter.Should().BeNull("identity users must remain queryable during anonymous auth and provisioning flows");
    }

    [Fact]
    public void OnModelCreating_Applies_CreatorFilter_To_NonIdentity_MEntity_Types()
    {
        DbContextOptions<FilterTestDbContext> options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using FilterTestDbContext db = new(options);
        IModel model = db.Model;

        IEntityType? ownedType = model.FindEntityType(typeof(OwnedEntity));
        ownedType.Should().NotBeNull();

        System.Linq.Expressions.LambdaExpression? filter = ownedType!.GetQueryFilter();
        filter.Should().NotBeNull("application-owned entities should continue using creator isolation by default");
    }

    [Fact]
    public void OnModelCreating_Keeps_NonIdentity_CreatorFilter_When_Identity_Filters_Are_Exempted()
    {
        DbContextOptions<FilterTestDbContext> options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using FilterTestDbContext db = new(options);
        IModel model = db.Model;

        IEntityType? userType = model.FindEntityType(typeof(MUser));
        IEntityType? ownedType = model.FindEntityType(typeof(OwnedEntity));

        userType.Should().NotBeNull();
        ownedType.Should().NotBeNull();
        userType!.GetQueryFilter().Should().BeNull();
        ownedType!.GetQueryFilter().Should().NotBeNull();
    }

    [Fact]
    public void OnModelCreating_Applies_UtcDateTime_Converters()
    {
        DbContextOptions<FilterTestDbContext> options = new DbContextOptionsBuilder<FilterTestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using FilterTestDbContext db = new(options);
        IModel model = db.Model;

        IEntityType? userType = model.FindEntityType(typeof(MUser));
        IProperty? creationTime = userType!.FindProperty("CreationTime");
        creationTime.Should().NotBeNull();
        creationTime!.GetValueConverter().Should().NotBeNull();
    }
}
