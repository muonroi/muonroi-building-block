using Muonroi.Tenancy.SiteProfile.Web.Dapper;

namespace TestProject.Service.Sites.Bravo;

/// <summary>
/// BRAVO custom column map — demonstrates all three ISiteColumnMap capabilities:
/// 1. Column renaming (BookingNo → BOOKING_NUMBER)
/// 2. Column removal (LegacyField excluded from queries)
/// 3. Extra columns (BRAVO_TRACKING_REF added to site-specific queries)
///
/// Register in DI:
/// <code>
/// services.AddKeyedSingleton&lt;ISiteColumnMap, BravoColumnMap&gt;("BRAVO");
/// services.AddSiteResolvedService&lt;ISiteColumnMap&gt;();
/// </code>
/// </summary>
public sealed class BravoColumnMap : DefaultSiteColumnMap
{
    private static readonly SiteExtraColumn[] s_extras =
    [
        new("TrackingReference", "BRAVO_TRACKING_REF", typeof(string)),
    ];

    /// <inheritdoc />
    public override string Column(string propertyName) => propertyName switch
    {
        // BRAVO stores BookingNo as BOOKING_NUMBER, not BOOKING_NO (convention)
        "BookingNo" => "BOOKING_NUMBER",
        // All other properties use the UPPER_SNAKE_CASE convention from base class
        _ => base.Column(propertyName)
    };

    /// <inheritdoc />
    public override string Column(string propertyName, string tableName)
        => (propertyName, tableName) switch
        {
            // BRAVO stores BookingNo differently in ORDER_DETAIL vs other tables
            ("BookingNo", "ORDER_DETAIL") => "BRAVO_ORDER_BKG",
            ("BookingNo", _) => "BOOKING_NUMBER",  // global BRAVO override
            _ => base.Column(propertyName)
        };

    /// <inheritdoc />
    public override bool HasColumn(string propertyName) => propertyName != "LegacyField";

    /// <inheritdoc />
    public override IReadOnlyList<SiteExtraColumn> ExtraColumns => s_extras;
}
