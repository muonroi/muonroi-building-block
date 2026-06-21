namespace Muonroi.Billing.Abstractions;

/// <summary>
/// A priced rollup of metered usage for one dimension over a billing period (MON-01).
/// <para>
/// <see cref="Amount"/> is the deterministic per-unit product <c>Quantity * UnitRate</c>
/// (D-05: simple per-unit × tier rate; no proration, tax, or multi-currency). This type is a
/// pure compute model and never triggers an external call or a charge.
/// </para>
/// </summary>
/// <param name="Dimension">The metered dimension this line item rolls up.</param>
/// <param name="Quantity">The total metered quantity for the dimension in the period.</param>
/// <param name="UnitRate">The per-unit rate applied to <paramref name="Quantity"/>.</param>
/// <param name="Amount">The computed amount (<c>Quantity * UnitRate</c>).</param>
/// <param name="Description">An optional human-readable description of the line item.</param>
public sealed record UsageLineItem(
    QuotaType Dimension,
    long Quantity,
    decimal UnitRate,
    decimal Amount,
    string? Description = null)
{
    /// <summary>
    /// The canonical description used for the optional flat tier base line item (D-05).
    /// Aggregator implementations (17-02) emit the flat-base line using this constant so the
    /// preview/invoice surface can identify it deterministically.
    /// </summary>
    public const string FlatBaseDescription = "Flat tier base";

    /// <summary>
    /// Creates a <see cref="UsageLineItem"/> computing <see cref="Amount"/> as
    /// <paramref name="quantity"/> * <paramref name="unitRate"/> (D-05 deterministic arithmetic).
    /// </summary>
    /// <param name="dimension">The metered dimension.</param>
    /// <param name="quantity">The metered quantity.</param>
    /// <param name="unitRate">The per-unit rate.</param>
    /// <param name="description">An optional description.</param>
    /// <returns>A line item whose amount is the product of quantity and unit rate.</returns>
    public static UsageLineItem Create(
        QuotaType dimension,
        long quantity,
        decimal unitRate,
        string? description = null)
        => new(dimension, quantity, unitRate, quantity * unitRate, description);
}
