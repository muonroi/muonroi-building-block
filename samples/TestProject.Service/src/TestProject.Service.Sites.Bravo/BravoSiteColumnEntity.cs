using Muonroi.EntityFrameworkCore.Configuration;

namespace TestProject.Service.Sites.Bravo;

/// <summary>
/// Demonstrates [SiteColumn] attribute-based EF column mapping (per D-08).
/// BRAVO uses explicit column name overrides instead of virtual ConfigureSiteColumns.
/// Properties without [SiteColumn] map to UPPER_SNAKE_CASE by convention when
/// <see cref="SiteColumnExtensions.ApplySiteColumnOverrides{TEntity}"/> is called.
/// </summary>
public class BravoOrder
{
    /// <summary>
    /// Surrogate primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Booking reference — explicitly mapped to BOOKING_NUMBER (not BOOKING_NO by convention).
    /// Demonstrates Name + MaxLength overrides via [SiteColumn].
    /// </summary>
    [SiteColumn(Name = "BOOKING_NUMBER", MaxLength = 25)]
    public string? BookingNo { get; set; }

    /// <summary>
    /// Order status — required column with a default value of "N" (New).
    /// Demonstrates IsRequired + DefaultValue overrides via [SiteColumn].
    /// </summary>
    [SiteColumn(IsRequired = true, DefaultValue = "N")]
    public string? Status { get; set; }

    /// <summary>
    /// Container number — no [SiteColumn] attribute → convention applies: CONTAINER_NO.
    /// Demonstrates that absence of the attribute still applies UPPER_SNAKE_CASE convention.
    /// </summary>
    public string? ContainerNo { get; set; }
}
