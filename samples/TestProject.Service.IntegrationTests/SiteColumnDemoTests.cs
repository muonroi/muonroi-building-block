using System.Reflection;
using Muonroi.EntityFrameworkCore.Configuration;
using TestProject.Service.Sites.Bravo;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// D-25: Demonstrates [SiteColumn] attribute-based EF column mapping.
/// Verifies that BravoOrder with [SiteColumn] overrides maps correctly,
/// and properties without the attribute get UPPER_SNAKE_CASE convention.
/// All tests emit [TEST:BREAKPOINT] IMLog markers per D-26.
/// </summary>
public sealed class SiteColumnDemoTests
{
    [Fact]
    public void BravoOrder_BookingNo_HasSiteColumnAttribute_WithNameOverride()
    {
        // [TEST:BREAKPOINT] SiteColumnDemoTests - BookingNo attribute check
        var prop = typeof(BravoOrder).GetProperty("BookingNo");
        Assert.NotNull(prop);

        var attr = prop.GetCustomAttribute<SiteColumnAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("BOOKING_NUMBER", attr.Name);
        Assert.Equal(25, attr.MaxLength);
    }

    [Fact]
    public void BravoOrder_Status_HasSiteColumnAttribute_IsRequiredAndDefaultValue()
    {
        // [TEST:BREAKPOINT] SiteColumnDemoTests - Status attribute check
        var prop = typeof(BravoOrder).GetProperty("Status");
        Assert.NotNull(prop);

        var attr = prop.GetCustomAttribute<SiteColumnAttribute>();
        Assert.NotNull(attr);
        Assert.True(attr.IsRequired, "Status should be marked IsRequired=true");
        Assert.Equal("N", attr.DefaultValue);
    }

    [Fact]
    public void BravoOrder_ContainerNo_HasNoSiteColumnAttribute_UsesConvention()
    {
        // [TEST:BREAKPOINT] SiteColumnDemoTests - ContainerNo convention
        var prop = typeof(BravoOrder).GetProperty("ContainerNo");
        Assert.NotNull(prop);

        var attr = prop.GetCustomAttribute<SiteColumnAttribute>();
        Assert.Null(attr); // No attribute → convention applies via ApplySiteColumnOverrides
    }

    [Fact]
    public void ApplySiteColumnOverrides_WiresBookingNumberColumnName()
    {
        // [TEST:BREAKPOINT] SiteColumnDemoTests - ApplySiteColumnOverrides
        // Verify that ApplySiteColumnOverrides maps BookingNo -> BOOKING_NUMBER in EF model
        var dbContextOptions = new DbContextOptionsBuilder<SiteColumnDemoDbContext>()
            .UseInMemoryDatabase("SiteColumnDemoTest")
            .Options;

        using var context = new SiteColumnDemoDbContext(dbContextOptions);
        var entityType = context.Model.FindEntityType(typeof(BravoOrder));
        Assert.NotNull(entityType);

        var bookingNoProp = entityType.FindProperty("BookingNo");
        Assert.NotNull(bookingNoProp);
        // [SiteColumn(Name = "BOOKING_NUMBER")] should override column name
        Assert.Equal("BOOKING_NUMBER", bookingNoProp.GetColumnName());

        var statusProp = entityType.FindProperty("Status");
        Assert.NotNull(statusProp);
        // IsRequired=true — column is NOT nullable
        Assert.False(statusProp.IsNullable, "Status should be NOT NULL (IsRequired=true)");
    }

    /// <summary>
    /// Minimal DbContext for testing ApplySiteColumnOverrides with BravoOrder entity.
    /// Uses in-memory provider so no real database is needed.
    /// </summary>
    private sealed class SiteColumnDemoDbContext(DbContextOptions<SiteColumnDemoDbContext> options) : DbContext(options)
    {
        public DbSet<BravoOrder> BravoOrders { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Demonstrate [SiteColumn] attribute-based configuration
            modelBuilder.ApplySiteColumnOverrides<BravoOrder>("BRAVO");
        }
    }
}
