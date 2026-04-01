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
    // SiteSqlBuilder.Col — wraps ISiteColumnMap.Column directly
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSqlBuilder_Col_ReturnsColumnName()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        Assert.Equal("SITE_CODE", builder.Col("SiteCode"));
    }

    // -----------------------------------------------------------------------
    // SiteSqlBuilder.Interpolate — alias.COLUMN AS Property regression
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSqlBuilder_Interpolate_DefaultMap_ReplacesColumnNames()
    {
        // Interpolate replaces the literal column in SQL with _columnMap.Column(propertyName)
        // DefaultSiteColumnMap: "ContainerNo" → "CONTAINER_NO", "BookingNo" → "BOOKING_NO"
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        const string sql = "SELECT od.ITEM_NO AS ContainerNo, od.BOOKING_NO AS BookingNo FROM ORDER_DETAIL od";

        string result = builder.Interpolate(sql);

        // Columns replaced with site-specific names derived from property names:
        // "ContainerNo" → CONTAINER_NO, "BookingNo" → BOOKING_NO
        Assert.Equal("SELECT od.CONTAINER_NO AS ContainerNo, od.BOOKING_NO AS BookingNo FROM ORDER_DETAIL od", result);
    }

    [Fact]
    public void SiteSqlBuilder_Interpolate_CustomMap_ReplacesColumn()
    {
        var builder = new SiteSqlBuilder(new TciSiteColumnMap());
        const string sql = "SELECT od.BOOKING_NO AS BookingNo FROM ORDER_DETAIL od";

        string result = builder.Interpolate(sql);

        // TciSiteColumnMap overrides "BookingNo" → "TCI_BOOKING_EXT"
        Assert.Equal("SELECT od.TCI_BOOKING_EXT AS BookingNo FROM ORDER_DETAIL od", result);
    }

    // -----------------------------------------------------------------------
    // SiteSqlBuilder.InterpolateMarkers — [[PropertyName]] marker syntax
    // -----------------------------------------------------------------------

    [Fact]
    public void InterpolateMarkers_SingleMarker_WhereClause_ReplacesCorrectly()
    {
        // Test 1: single marker in WHERE clause
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        const string sql = "WHERE od.[[SiteCode]] = @siteCode";

        string result = builder.InterpolateMarkers(sql);

        Assert.Equal("WHERE od.SITE_CODE = @siteCode", result);
    }

    [Fact]
    public void InterpolateMarkers_MultipleMarkers_JoinOn_ReplacesAll()
    {
        // Test 2: multiple markers in JOIN ON clause
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        const string sql = "JOIN details d ON d.[[BookingNo]] = h.[[SiteCode]]";

        string result = builder.InterpolateMarkers(sql);

        Assert.Equal("JOIN details d ON d.BOOKING_NO = h.SITE_CODE", result);
    }

    [Fact]
    public void InterpolateMarkers_CaseExpression_ResolvesMarker()
    {
        // Test 3: marker inside CASE expression
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        const string sql = "CASE WHEN [[BookingNo]] = '' THEN 0 END";

        string result = builder.InterpolateMarkers(sql);

        Assert.Equal("CASE WHEN BOOKING_NO = '' THEN 0 END", result);
    }

    [Fact]
    public void InterpolateMarkers_Ctebody_ResolvesAllMarkers()
    {
        // Test 4: CTE body containing multiple markers
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        const string sql = "WITH cte AS (SELECT [[SiteCode]], [[BookingNo]] FROM orders)";

        string result = builder.InterpolateMarkers(sql);

        Assert.Equal("WITH cte AS (SELECT SITE_CODE, BOOKING_NO FROM orders)", result);
    }

    [Fact]
    public void InterpolateMarkers_CustomColumnMap_ProducesSiteSpecificColumn()
    {
        // Test 5: custom ISiteColumnMap overrides "BookingNo" to "TCI_BOOKING_EXT"
        var builder = new SiteSqlBuilder(new TciSiteColumnMap());
        const string sql = "WHERE [[BookingNo]] = @bookingNo";

        string result = builder.InterpolateMarkers(sql);

        Assert.Equal("WHERE TCI_BOOKING_EXT = @bookingNo", result);
    }

    [Fact]
    public void InterpolateMarkers_NoMarkers_ReturnsSqlUnchanged()
    {
        // Test 6: passthrough when no markers present
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        const string sql = "SELECT * FROM orders WHERE status = @status";

        string result = builder.InterpolateMarkers(sql);

        Assert.Equal(sql, result);
    }

    [Fact]
    public void InterpolateMarkers_NullInput_ThrowsArgumentNullException()
    {
        // Test 7: null input throws ArgumentNullException
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());

        Assert.Throws<ArgumentNullException>(() => builder.InterpolateMarkers(null!));
    }

    [Fact]
    public void InterpolateMarkers_PreservesNonMarkerContent()
    {
        // Test 8: whitespace, keywords, literals, parameters preserved unchanged
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        const string sql = "SELECT   id, [[ContainerNo]], @param, 'literal' FROM t WHERE x = 1";

        string result = builder.InterpolateMarkers(sql);

        Assert.Equal("SELECT   id, CONTAINER_NO, @param, 'literal' FROM t WHERE x = 1", result);
    }

    [Fact]
    public void InterpolateMarkers_GroupBy_ResolvesBothMarkers()
    {
        // Test 10: GROUP BY with multiple markers
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        const string sql = "GROUP BY [[SiteCode]], [[BookingNo]]";

        string result = builder.InterpolateMarkers(sql);

        Assert.Equal("GROUP BY SITE_CODE, BOOKING_NO", result);
    }

    [Fact]
    public void InterpolateMarkers_CustomMap_FallsBackToDefaultForNonOverridden()
    {
        // Custom map only overrides BookingNo; SiteCode should still use default convention
        var builder = new SiteSqlBuilder(new TciSiteColumnMap());
        const string sql = "WHERE [[SiteCode]] = @s AND [[BookingNo]] = @b";

        string result = builder.InterpolateMarkers(sql);

        Assert.Equal("WHERE SITE_CODE = @s AND TCI_BOOKING_EXT = @b", result);
    }

    [Fact]
    public void InterpolateMarkers_OldDoubleBraceSyntax_NotResolved()
    {
        // Regression: old {{}} syntax should pass through unchanged (not resolved)
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        const string sql = "WHERE {{BookingNo}} = @b";
        string result = builder.InterpolateMarkers(sql);
        // Old {{}} syntax should pass through unchanged
        Assert.Equal(sql, result);
    }

    // -----------------------------------------------------------------------
    // HasColumn — column removal per site
    // -----------------------------------------------------------------------

    [Fact]
    public void DefaultSiteColumnMap_HasColumn_ReturnsTrue()
    {
        var sut = new DefaultSiteColumnMap();
        Assert.True(sut.HasColumn("AnyProperty"));
    }

    [Fact]
    public void DefaultSiteColumnMap_ExtraColumns_ReturnsEmpty()
    {
        var sut = new DefaultSiteColumnMap();
        Assert.Empty(sut.ExtraColumns);
    }

    [Fact]
    public void SiteSqlBuilder_Select_SkipsColumnsWhereHasColumnFalse()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());

        // ContainerNo is removed by HasColumn override
        string result = builder.Select("BookingNo", "ContainerNo", "SiteCode");

        Assert.Equal("BOOKING_NO AS BookingNo, SITE_CODE AS SiteCode", result);
        Assert.DoesNotContain("ContainerNo", result);
    }

    [Fact]
    public void SiteSqlBuilder_Select_AllColumnsFilteredOut_ThrowsArgumentException()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());

        // Only ContainerNo provided, which is removed
        Assert.Throws<ArgumentException>(() => builder.Select("ContainerNo"));
    }

    [Fact]
    public void SiteSqlBuilder_SelectFrom_SkipsRemovedColumns()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());

        string result = builder.SelectFrom("orders", "BookingNo", "ContainerNo");

        Assert.Equal("SELECT BOOKING_NO AS BookingNo FROM orders", result);
    }

    // -----------------------------------------------------------------------
    // ExtraColumns — column addition per site
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSqlBuilder_SelectWithExtras_AppendsExtraColumns()
    {
        var builder = new SiteSqlBuilder(new SiteWithExtraColumnsMap());

        string result = builder.SelectWithExtras("BookingNo", "ContainerNo");

        Assert.Equal("BOOKING_NO AS BookingNo, CONTAINER_NO AS ContainerNo, TRACKING_REF AS TrackingRef", result);
    }

    [Fact]
    public void SiteSqlBuilder_SelectFromWithExtras_GeneratesFullStatement()
    {
        var builder = new SiteSqlBuilder(new SiteWithExtraColumnsMap());

        string result = builder.SelectFromWithExtras("orders", "BookingNo");

        Assert.Equal("SELECT BOOKING_NO AS BookingNo, TRACKING_REF AS TrackingRef FROM orders", result);
    }

    [Fact]
    public void SiteSqlBuilder_SelectWithExtras_RemovedBaseButHasExtras_ReturnsExtrasOnly()
    {
        var builder = new SiteSqlBuilder(new SiteWithBothMap());

        // ContainerNo removed, but TrackingRef extra exists
        string result = builder.SelectWithExtras("ContainerNo");

        Assert.Equal("TRACKING_REF AS TrackingRef", result);
    }

    [Fact]
    public void SiteSqlBuilder_SelectWithExtras_CombinesFilteredBaseAndExtras()
    {
        var builder = new SiteSqlBuilder(new SiteWithBothMap());

        string result = builder.SelectWithExtras("BookingNo", "ContainerNo", "SiteCode");

        // ContainerNo removed, BookingNo + SiteCode kept, TrackingRef extra appended
        Assert.Equal("BOOKING_NO AS BookingNo, SITE_CODE AS SiteCode, TRACKING_REF AS TrackingRef", result);
    }

    [Fact]
    public void SiteSqlBuilder_SelectWithExtras_NoSurvivingColumnsNoExtras_ThrowsArgumentException()
    {
        // SiteWithRemovedColumnMap removes ContainerNo but has NO extras
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());

        Assert.Throws<ArgumentException>(() => builder.SelectWithExtras("ContainerNo"));
    }

    [Fact]
    public void SiteSqlBuilder_SelectWithExtras_EmptyPropertyList_ThrowsArgumentException()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());

        Assert.Throws<ArgumentException>(() => builder.SelectWithExtras());
    }

    [Fact]
    public void SiteSqlBuilder_SelectWithExtras_DefaultMap_NoExtras_BehavesLikeSelect()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());

        string selectResult = builder.Select("BookingNo", "ContainerNo");
        string extrasResult = builder.SelectWithExtras("BookingNo", "ContainerNo");

        // No extras on default map → identical output
        Assert.Equal(selectResult, extrasResult);
    }

    // -----------------------------------------------------------------------
    // SiteExtraColumn record
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteExtraColumn_DefaultIsNullable_IsTrue()
    {
        var col = new SiteExtraColumn("Prop", "COL", typeof(string));
        Assert.True(col.IsNullable);
    }

    [Fact]
    public void SiteExtraColumn_ExplicitIsNullableFalse_Works()
    {
        var col = new SiteExtraColumn("Prop", "COL", typeof(int), IsNullable: false);
        Assert.False(col.IsNullable);
    }

    // -----------------------------------------------------------------------
    // SiteSqlBuilder.HasColumn — wrapper
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSqlBuilder_HasColumn_DefaultMap_ReturnsTrue()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        Assert.True(builder.HasColumn("AnyProperty"));
    }

    [Fact]
    public void SiteSqlBuilder_HasColumn_RemovedColumn_ReturnsFalse()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());
        Assert.False(builder.HasColumn("ContainerNo"));
        Assert.True(builder.HasColumn("BookingNo"));
    }

    // -----------------------------------------------------------------------
    // SiteSqlBuilder.ColOrNull
    // -----------------------------------------------------------------------

    [Fact]
    public void SiteSqlBuilder_ColOrNull_ExistingColumn_ReturnsColumnName()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        Assert.Equal("BOOKING_NO", builder.ColOrNull("BookingNo"));
    }

    [Fact]
    public void SiteSqlBuilder_ColOrNull_RemovedColumn_ReturnsNull()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());
        Assert.Null(builder.ColOrNull("ContainerNo"));
    }

    [Fact]
    public void SiteSqlBuilder_ColOrNull_CustomMap_ResolvesCorrectly()
    {
        var builder = new SiteSqlBuilder(new TciSiteColumnMap());
        Assert.Equal("TCI_BOOKING_EXT", builder.ColOrNull("BookingNo"));
    }

    // -----------------------------------------------------------------------
    // InterpolateMarkers — fail-fast on removed columns
    // -----------------------------------------------------------------------

    [Fact]
    public void InterpolateMarkers_RemovedColumn_ThrowsInvalidOperationException()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());
        const string sql = "WHERE [[ContainerNo]] = @c";

        var ex = Assert.Throws<InvalidOperationException>(() => builder.InterpolateMarkers(sql));
        Assert.Contains("ContainerNo", ex.Message);
        Assert.Contains("InterpolateMarkersSafe", ex.Message);
    }

    [Fact]
    public void InterpolateMarkers_MixedExistingAndRemoved_ThrowsOnFirstRemoved()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());
        // BookingNo exists, ContainerNo removed
        const string sql = "SELECT [[BookingNo]], [[ContainerNo]] FROM orders";

        Assert.Throws<InvalidOperationException>(() => builder.InterpolateMarkers(sql));
    }

    [Fact]
    public void InterpolateMarkers_AllColumnsExist_WorksUnchanged()
    {
        // Regression: existing behavior unchanged when all columns exist
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());
        const string sql = "WHERE [[BookingNo]] = @b AND [[SiteCode]] = @s";

        string result = builder.InterpolateMarkers(sql);

        Assert.Equal("WHERE BOOKING_NO = @b AND SITE_CODE = @s", result);
    }

    // -----------------------------------------------------------------------
    // InterpolateMarkersSafe — fallback for removed columns
    // -----------------------------------------------------------------------

    [Fact]
    public void InterpolateMarkersSafe_RemovedColumn_DefaultFallback_ReplacesWithNull()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());
        const string sql = "SELECT [[BookingNo]], [[ContainerNo]] FROM orders";

        string result = builder.InterpolateMarkersSafe(sql);

        Assert.Equal("SELECT BOOKING_NO, NULL FROM orders", result);
    }

    [Fact]
    public void InterpolateMarkersSafe_RemovedColumn_CustomFallback()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());
        const string sql = "SELECT [[ContainerNo]] FROM orders";

        string result = builder.InterpolateMarkersSafe(sql, "'N/A'");

        Assert.Equal("SELECT 'N/A' FROM orders", result);
    }

    [Fact]
    public void InterpolateMarkersSafe_AllColumnsExist_BehavesLikeInterpolateMarkers()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        const string sql = "WHERE [[BookingNo]] = @b";

        string strict = builder.InterpolateMarkers(sql);
        string safe = builder.InterpolateMarkersSafe(sql);

        Assert.Equal(strict, safe);
    }

    [Fact]
    public void InterpolateMarkersSafe_WhereClause_RemovedColumn_FallbackInPredicate()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());
        // WHERE with removed column → NULL = @c → always false, effectively skips condition
        const string sql = "WHERE [[ContainerNo]] = @c AND [[BookingNo]] = @b";

        string result = builder.InterpolateMarkersSafe(sql);

        Assert.Equal("WHERE NULL = @c AND BOOKING_NO = @b", result);
    }

    [Fact]
    public void InterpolateMarkersSafe_NullInput_ThrowsArgumentNullException()
    {
        var builder = new SiteSqlBuilder(new DefaultSiteColumnMap());
        Assert.Throws<ArgumentNullException>(() => builder.InterpolateMarkersSafe(null!));
    }

    [Fact]
    public void InterpolateMarkersSafe_NoMarkers_ReturnsSqlUnchanged()
    {
        var builder = new SiteSqlBuilder(new SiteWithRemovedColumnMap());
        const string sql = "SELECT * FROM orders";

        string result = builder.InterpolateMarkersSafe(sql);

        Assert.Equal(sql, result);
    }

    [Fact]
    public void InterpolateMarkersSafe_MultipleRemovedColumns_AllReplacedWithFallback()
    {
        // Site that removes both ContainerNo and LegacyField
        var builder = new SiteSqlBuilder(new SiteWithMultiRemovedMap());
        const string sql = "SELECT [[BookingNo]], [[ContainerNo]], [[LegacyField]] FROM orders";

        string result = builder.InterpolateMarkersSafe(sql, "0");

        Assert.Equal("SELECT BOOKING_NO, 0, 0 FROM orders", result);
    }

    // -----------------------------------------------------------------------
    // Helper test implementations
    // -----------------------------------------------------------------------

    private sealed class CustomBookingColumnMap : DefaultSiteColumnMap
    {
        public override string Column(string propertyName)
            => propertyName == "BookingNo" ? "BOOKING_NUMBER" : base.Column(propertyName);
    }

    /// <summary>
    /// Simulates a site-specific column map that overrides "BookingNo" to a non-default name.
    /// </summary>
    private sealed class TciSiteColumnMap : DefaultSiteColumnMap
    {
        public override string Column(string propertyName)
            => propertyName == "BookingNo" ? "TCI_BOOKING_EXT" : base.Column(propertyName);
    }

    /// <summary>
    /// Site that removes ContainerNo from queries (column doesn't exist in this site's schema).
    /// </summary>
    private sealed class SiteWithRemovedColumnMap : DefaultSiteColumnMap
    {
        public override bool HasColumn(string propertyName) => propertyName != "ContainerNo";
    }

    /// <summary>
    /// Site that adds an extra TrackingRef column not in the base entity.
    /// </summary>
    private sealed class SiteWithExtraColumnsMap : DefaultSiteColumnMap
    {
        private static readonly SiteExtraColumn[] s_extras =
        [
            new("TrackingRef", "TRACKING_REF", typeof(string)),
        ];

        public override IReadOnlyList<SiteExtraColumn> ExtraColumns => s_extras;
    }

    /// <summary>
    /// Site that removes ContainerNo AND adds extra TrackingRef.
    /// </summary>
    private sealed class SiteWithBothMap : DefaultSiteColumnMap
    {
        private static readonly SiteExtraColumn[] s_extras =
        [
            new("TrackingRef", "TRACKING_REF", typeof(string)),
        ];

        public override bool HasColumn(string propertyName) => propertyName != "ContainerNo";
        public override IReadOnlyList<SiteExtraColumn> ExtraColumns => s_extras;
    }

    /// <summary>
    /// Site that removes multiple columns (ContainerNo and LegacyField).
    /// </summary>
    private sealed class SiteWithMultiRemovedMap : DefaultSiteColumnMap
    {
        public override bool HasColumn(string propertyName)
            => propertyName is not ("ContainerNo" or "LegacyField");
    }
}
