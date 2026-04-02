using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Muonroi.EntityFrameworkCore.Configuration.Tests;

/// <summary>
/// Tests for [SiteColumn] attribute and ApplySiteColumnOverrides extension.
/// Covers TDD behaviors: attribute properties, convention mapping, and EF model builder integration.
/// </summary>
public class SiteColumnTests
{
    // ===========================
    // ToUpperSnakeCase unit tests
    // ===========================

    [Theory]
    [InlineData("BookingNo", "BOOKING_NO")]
    [InlineData("Id", "ID")]
    [InlineData("ContainerNumber", "CONTAINER_NUMBER")]
    [InlineData("VesselName", "VESSEL_NAME")]
    [InlineData("IsHazardous", "IS_HAZARDOUS")]
    [InlineData("OutVoyage", "OUT_VOYAGE")]
    [InlineData("CreatedDate", "CREATED_DATE")]
    [InlineData("UpdatedBy", "UPDATED_BY")]
    [InlineData("UnNo", "UN_NO")]
    [InlineData("A", "A")]
    [InlineData("ABC", "ABC")]
    public void ToUpperSnakeCase_ConvertsPascalCaseCorrectly(string input, string expected)
    {
        var result = SiteColumnExtensions.ToUpperSnakeCase(input);
        result.Should().Be(expected);
    }

    // ===========================
    // SiteColumnAttribute property tests
    // ===========================

    [Fact]
    public void SiteColumnAttribute_DefaultValues_AreCorrect()
    {
        var attr = new SiteColumnAttribute();
        attr.Name.Should().BeNull();
        attr.MaxLength.Should().Be(-1);
        attr.IsRequired.Should().BeFalse();
        attr.DefaultValue.Should().BeNull();
        attr.HasColumnType.Should().BeNull();
        attr.Ignore.Should().BeFalse();
    }

    [Fact]
    public void SiteColumnAttribute_CanBeAppliedToProperty()
    {
        var prop = typeof(EntityWithSiteColumns)
            .GetProperty(nameof(EntityWithSiteColumns.BookingNumber))!;
        var attr = prop.GetCustomAttribute<SiteColumnAttribute>();
        attr.Should().NotBeNull();
        attr!.Name.Should().Be("BOOKING_NUMBER");
    }

    [Fact]
    public void SiteColumnAttribute_MaxLength_IsReadFromProperty()
    {
        var prop = typeof(EntityWithSiteColumns)
            .GetProperty(nameof(EntityWithSiteColumns.ContainerNo))!;
        var attr = prop.GetCustomAttribute<SiteColumnAttribute>();
        attr.Should().NotBeNull();
        attr!.MaxLength.Should().Be(25);
    }

    [Fact]
    public void SiteColumnAttribute_IsRequired_IsReadFromProperty()
    {
        var prop = typeof(EntityWithSiteColumns)
            .GetProperty(nameof(EntityWithSiteColumns.RequiredField))!;
        var attr = prop.GetCustomAttribute<SiteColumnAttribute>();
        attr.Should().NotBeNull();
        attr!.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void SiteColumnAttribute_Ignore_IsReadFromProperty()
    {
        var prop = typeof(EntityWithSiteColumns)
            .GetProperty(nameof(EntityWithSiteColumns.IgnoredProp))!;
        var attr = prop.GetCustomAttribute<SiteColumnAttribute>();
        attr.Should().NotBeNull();
        attr!.Ignore.Should().BeTrue();
    }

    [Fact]
    public void SiteColumnAttribute_HasColumnType_IsReadFromProperty()
    {
        var prop = typeof(EntityWithSiteColumns)
            .GetProperty(nameof(EntityWithSiteColumns.Weight))!;
        var attr = prop.GetCustomAttribute<SiteColumnAttribute>();
        attr.Should().NotBeNull();
        attr!.HasColumnType.Should().Be("decimal(18,4)");
    }

    [Fact]
    public void SiteColumnAttribute_DefaultValue_IsReadFromProperty()
    {
        var prop = typeof(EntityWithSiteColumns)
            .GetProperty(nameof(EntityWithSiteColumns.Status))!;
        var attr = prop.GetCustomAttribute<SiteColumnAttribute>();
        attr.Should().NotBeNull();
        attr!.DefaultValue.Should().Be("N");
    }

    // ===========================
    // EF ModelBuilder integration tests via InMemory
    // ===========================

    [Fact]
    public void ApplySiteColumnOverrides_NameOverride_SetsHasColumnName()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var ctx = new TestDbContext(options);
        var entityType = ctx.Model.FindEntityType(typeof(EntityWithSiteColumns))!;
        var prop = entityType.FindProperty(nameof(EntityWithSiteColumns.BookingNumber))!;
        prop.GetColumnName().Should().Be("BOOKING_NUMBER");
    }

    [Fact]
    public void ApplySiteColumnOverrides_MaxLength_SetsHasMaxLength()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var ctx = new TestDbContext(options);
        var entityType = ctx.Model.FindEntityType(typeof(EntityWithSiteColumns))!;
        var prop = entityType.FindProperty(nameof(EntityWithSiteColumns.ContainerNo))!;
        prop.GetMaxLength().Should().Be(25);
    }

    [Fact]
    public void ApplySiteColumnOverrides_NoAttribute_UsesUpperSnakeCaseConvention()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var ctx = new TestDbContext(options);
        var entityType = ctx.Model.FindEntityType(typeof(EntityWithSiteColumns))!;
        var prop = entityType.FindProperty(nameof(EntityWithSiteColumns.VesselName))!;
        prop.GetColumnName().Should().Be("VESSEL_NAME");
    }

    [Fact]
    public void ApplySiteColumnOverrides_CombinedNameAndMaxLength_AppliesBoth()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var ctx = new TestDbContext(options);
        var entityType = ctx.Model.FindEntityType(typeof(EntityWithSiteColumns))!;
        var prop = entityType.FindProperty(nameof(EntityWithSiteColumns.ContainerNo))!;
        // ContainerNo has [SiteColumn(MaxLength=25)] only — no Name override, so UPPER_SNAKE_CASE convention applies
        prop.GetColumnName().Should().Be("CONTAINER_NO");
        prop.GetMaxLength().Should().Be(25);
    }

    [Fact]
    public void ApplySiteColumnOverrides_AttributeUsage_IsPropertyOnly()
    {
        var usage = typeof(SiteColumnAttribute).GetCustomAttribute<AttributeUsageAttribute>();
        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Property);
        usage.AllowMultiple.Should().BeFalse();
    }
}

// ===========================
// Test helpers
// ===========================

/// <summary>
/// Test entity with various [SiteColumn] configurations.
/// </summary>
public class EntityWithSiteColumns
{
    public int Id { get; set; }

    [SiteColumn(Name = "BOOKING_NUMBER")]
    public string? BookingNumber { get; set; }

    [SiteColumn(MaxLength = 25)]
    public string? ContainerNo { get; set; }

    [SiteColumn(IsRequired = true)]
    public string? RequiredField { get; set; }

    [SiteColumn(Ignore = true)]
    public string? IgnoredProp { get; set; }

    [SiteColumn(HasColumnType = "decimal(18,4)")]
    public decimal? Weight { get; set; }

    [SiteColumn(DefaultValue = "N")]
    public string? Status { get; set; }

    // No attribute — should map to VESSEL_NAME via convention
    public string? VesselName { get; set; }
}

/// <summary>
/// Test DbContext that calls ApplySiteColumnOverrides.
/// </summary>
public class TestDbContext : DbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

    public DbSet<EntityWithSiteColumns> Entities { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<EntityWithSiteColumns>().HasKey(e => e.Id);
        modelBuilder.ApplySiteColumnOverrides<EntityWithSiteColumns>("test-site");
    }
}
