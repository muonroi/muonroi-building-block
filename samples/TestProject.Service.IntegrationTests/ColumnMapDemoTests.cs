using TestProject.Service.Sites.Bravo;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// D-25: Demonstrates ISiteColumnMap + SiteSqlBuilder per-site column naming.
/// BravoColumnMap overrides BookingNo → BOOKING_NUMBER (vs BOOKING_NO convention).
/// DefaultSiteColumnMap applies UPPER_SNAKE_CASE convention for all properties.
/// All tests emit [TEST:BREAKPOINT] IMLog markers per D-26.
/// </summary>
public sealed class ColumnMapDemoTests
{
    [Fact]
    public void BravoColumnMap_BookingNo_ReturnsBOOKING_NUMBER()
    {
        // [TEST:BREAKPOINT] ColumnMapDemoTests - BookingNo override
        var map = new BravoColumnMap();
        Assert.Equal("BOOKING_NUMBER", map.Column("BookingNo"));
    }

    [Fact]
    public void BravoColumnMap_OtherProperties_UseConvention()
    {
        // [TEST:BREAKPOINT] ColumnMapDemoTests - Convention fallback
        var map = new BravoColumnMap();
        // ContainerNo → CONTAINER_NO by UPPER_SNAKE_CASE convention
        Assert.Equal("CONTAINER_NO", map.Column("ContainerNo"));
        // Status → STATUS by convention
        Assert.Equal("STATUS", map.Column("Status"));
        // CreatedAt → CREATED_AT by convention
        Assert.Equal("CREATED_AT", map.Column("CreatedAt"));
    }

    [Fact]
    public void DefaultSiteColumnMap_AllProperties_UseUpperSnakeCase()
    {
        // [TEST:BREAKPOINT] ColumnMapDemoTests - Default convention
        var map = new DefaultSiteColumnMap();
        Assert.Equal("BOOKING_NO", map.Column("BookingNo"));
        Assert.Equal("CONTAINER_NO", map.Column("ContainerNo"));
        Assert.Equal("STATUS", map.Column("Status"));
        Assert.Equal("ID", map.Column("Id"));
    }

    [Fact]
    public void SiteSqlBuilder_WithBravoColumnMap_GeneratesCorrectSelectFrom()
    {
        // [TEST:BREAKPOINT] ColumnMapDemoTests - SiteSqlBuilder BRAVO SQL
        var map = new BravoColumnMap();
        var builder = new SiteSqlBuilder(map);

        string sql = builder.SelectFrom("bookings", "BookingNo", "ContainerNo", "Status");

        // BRAVO: BookingNo → BOOKING_NUMBER; ContainerNo → CONTAINER_NO; Status → STATUS
        Assert.Contains("BOOKING_NUMBER AS BookingNo", sql);
        Assert.Contains("CONTAINER_NO AS ContainerNo", sql);
        Assert.Contains("STATUS AS Status", sql);
        Assert.StartsWith("SELECT", sql);
        Assert.Contains("FROM bookings", sql);
    }

    [Fact]
    public void SiteSqlBuilder_WithDefaultColumnMap_GeneratesConventionSql()
    {
        // [TEST:BREAKPOINT] ColumnMapDemoTests - SiteSqlBuilder DEFAULT SQL
        var map = new DefaultSiteColumnMap();
        var builder = new SiteSqlBuilder(map);

        string sql = builder.SelectFrom("bookings", "BookingNo", "ContainerNo");

        // DEFAULT convention: BookingNo → BOOKING_NO; ContainerNo → CONTAINER_NO
        Assert.Contains("BOOKING_NO AS BookingNo", sql);
        Assert.Contains("CONTAINER_NO AS ContainerNo", sql);
    }

    [Fact]
    public void SiteSqlBuilder_BravoVsDefault_DifferOnBookingNo()
    {
        // [TEST:BREAKPOINT] ColumnMapDemoTests - BRAVO vs DEFAULT comparison
        var bravoMap = new BravoColumnMap();
        var defaultMap = new DefaultSiteColumnMap();

        // The KEY difference: BRAVO uses BOOKING_NUMBER, DEFAULT uses BOOKING_NO
        Assert.NotEqual(bravoMap.Column("BookingNo"), defaultMap.Column("BookingNo"));
        Assert.Equal("BOOKING_NUMBER", bravoMap.Column("BookingNo"));
        Assert.Equal("BOOKING_NO", defaultMap.Column("BookingNo"));

        // But they agree on other columns
        Assert.Equal(bravoMap.Column("ContainerNo"), defaultMap.Column("ContainerNo"));
    }
}
