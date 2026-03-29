using Muonroi.Tenancy.SiteProfile.Web.Dapper;

namespace TestProject.Service.Sites.Bravo;

/// <summary>
/// BRAVO custom column map — overrides only the columns that differ from UPPER_SNAKE_CASE convention.
/// Demonstrates ISiteColumnMap pattern: subclass DefaultSiteColumnMap and override only what differs.
///
/// Register in DI:
/// <code>
/// services.AddKeyedSingleton&lt;ISiteColumnMap, BravoColumnMap&gt;("BRAVO");
/// services.AddSiteResolvedService&lt;ISiteColumnMap&gt;();
/// </code>
/// </summary>
public sealed class BravoColumnMap : DefaultSiteColumnMap
{
    /// <inheritdoc />
    public override string Column(string propertyName) => propertyName switch
    {
        // BRAVO stores BookingNo as BOOKING_NUMBER, not BOOKING_NO (convention)
        "BookingNo" => "BOOKING_NUMBER",
        // All other properties use the UPPER_SNAKE_CASE convention from base class
        _ => base.Column(propertyName)
    };
}
