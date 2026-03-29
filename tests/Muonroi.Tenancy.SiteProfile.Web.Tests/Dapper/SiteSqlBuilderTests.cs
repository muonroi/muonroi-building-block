using Muonroi.Tenancy.SiteProfile.Web.Dapper;

namespace Muonroi.Tenancy.SiteProfile.Web.Tests.Dapper;

/// <summary>
/// Unit tests for ISiteColumnMap, DefaultSiteColumnMap, and SiteSqlBuilder.
/// </summary>
public class SiteSqlBuilderTests
{
    // -----------------------------------------------------------------------
    // DefaultSiteColumnMap — PascalCase → UPPER_SNAKE_CASE
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("BookingNo", "BOOKING_NO")]
    [InlineData("Id", "ID")]
    [InlineData("ContainerNo", "CONTAINER_NO")]
    [InlineData("FullContainerDelivery", "FULL_CONTAINER_DELIVERY")]
    [InlineData("SiteId", "SITE_ID")]
    [InlineData("CreatedAt", "CREATED_AT")]
    public void DefaultSiteColumnMap_Column_ConvertsToUpperSnakeCase(string propertyName, string expected)
    {
        var sut = new DefaultSiteColumnMap();
        Assert.Equal(expected, sut.Column(propertyName));
    }

    [Fact]
    public void DefaultSiteColumnMap_Column_SingleWord_ReturnsUpperCase()
    {
        var sut = new DefaultSiteColumnMap();
        Assert.Equal("ID", sut.Column("Id"));
    }

    // -----------------------------------------------------------------------
    // Custom ISiteColumnMap override
    // -----------------------------------------------------------------------

    [Fact]
    public void CustomSiteColumnMap_Column_CanOverrideSpecificColumn()
    {
        // Arrange: custom map where "BookingNo" maps to "BOOKING_NUMBER" instead of convention
        var customMap = new CustomBookingColumnMap();

        // Act
        string result = customMap.Column("BookingNo");

        // Assert
        Assert.Equal("BOOKING_NUMBER", result);
    }

    [Fact]
    public void CustomSiteColumnMap_Column_FallsBackToDefaultForNonOverriddenColumns()
    {
        var customMap = new CustomBookingColumnMap();

        // "ContainerNo" not overridden, should use default convention
        Assert.Equal("CONTAINER_NO", customMap.Column("ContainerNo"));
    }

    // -----------------------------------------------------------------------
    // SiteSqlBuilder.Select — generates "COLUMN_NAME AS PropertyName" list
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSqlBuilder_Select_SingleProperty_ReturnsAlias()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());

        string result = builder.Select("BookingNo");

        Assert.Equal("BOOKING_NO AS BookingNo", result);
    }

    [Fact]
    public void SiteSqlBuilder_Select_MultipleProperties_ReturnsCommaSeparatedAliases()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());

        string result = builder.Select("BookingNo", "ContainerNo");

        Assert.Equal("BOOKING_NO AS BookingNo, CONTAINER_NO AS ContainerNo", result);
    }

    [Fact]
    public void SiteSqlBuilder_Select_ContainsAllPropertyAliases()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());

        string result = builder.Select("BookingNo", "ContainerNo");

        Assert.Contains("AS BookingNo", result);
        Assert.Contains("AS ContainerNo", result);
    }

    [Fact]
    public void SiteSqlBuilder_Select_WithCustomMap_UsesCustomColumnName()
    {
        var builder = new SiteSqlBuilder(new CustomBookingColumnMap());

        string result = builder.Select("BookingNo");

        Assert.Equal("BOOKING_NUMBER AS BookingNo", result);
    }

    [Fact]
    public void SiteSqlBuilder_Select_EmptyPropertyList_ThrowsArgumentException()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());

        Assert.Throws<ArgumentException>(() => builder.Select());
    }

    // -----------------------------------------------------------------------
    // SiteSqlBuilder.SelectFrom — generates full SELECT ... FROM ...
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSqlBuilder_SelectFrom_GeneratesFullSelectStatement()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());

        string result = builder.SelectFrom("orders", "BookingNo", "ContainerNo");

        Assert.Equal("SELECT BOOKING_NO AS BookingNo, CONTAINER_NO AS ContainerNo FROM orders", result);
    }

    [Fact]
    public void SiteSqlBuilder_SelectFrom_StartsWithSelect()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());

        string result = builder.SelectFrom("shipments", "Id", "SiteId");

        Assert.StartsWith("SELECT ", result);
        Assert.Contains("FROM shipments", result);
    }

    // -----------------------------------------------------------------------
    // SiteSqlBuilder constructor validation
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSqlBuilder_NullColumnMap_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SiteSqlBuilder(null!));
    }

    // -----------------------------------------------------------------------
    // Helper test implementations
    // -----------------------------------------------------------------------

    private sealed class CustomBookingColumnMap : DefaultSiteColumnMap
    {
        public override string Column(string propertyName)
            => propertyName == "BookingNo" ? "BOOKING_NUMBER" : base.Column(propertyName);
    }
}
