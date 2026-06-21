namespace Muonroi.Billing.Abstractions;

/// <summary>
/// A per-tier pricing model: per-unit rate per metered dimension plus an optional flat base (D-05).
/// <para>
/// Pricing is <c>Σ(quantity per dimension × per-unit rate) + optional flat base</c>. Deterministic,
/// unit-testable arithmetic only — no proration, tax, multi-currency, or dunning this phase.
/// </para>
/// </summary>
public sealed class PricingPlan
{
    /// <summary>
    /// Initializes a new <see cref="PricingPlan"/>.
    /// </summary>
    /// <param name="tier">The tenant tier this plan prices.</param>
    /// <param name="unitRates">
    /// The per-unit rate per metered dimension. May be empty; absent dimensions price at 0.
    /// </param>
    /// <param name="flatBaseAmount">The optional flat base charged for the period (default 0).</param>
    public PricingPlan(
        TenantTier tier,
        IReadOnlyDictionary<QuotaType, decimal>? unitRates = null,
        decimal flatBaseAmount = 0m)
    {
        Tier = tier;
        UnitRates = unitRates ?? new Dictionary<QuotaType, decimal>();
        FlatBaseAmount = flatBaseAmount;
    }

    /// <summary>Gets the tenant tier this plan prices.</summary>
    public TenantTier Tier { get; }

    /// <summary>Gets the optional flat base amount charged for the period (default 0).</summary>
    public decimal FlatBaseAmount { get; }

    /// <summary>Gets the configured per-unit rate per metered dimension.</summary>
    public IReadOnlyDictionary<QuotaType, decimal> UnitRates { get; }

    /// <summary>
    /// Gets the configured per-unit rate for <paramref name="dimension"/>, or <c>0m</c> when the
    /// dimension is not priced by this plan (D-05: unpriced dimensions contribute nothing).
    /// </summary>
    /// <param name="dimension">The metered dimension to look up.</param>
    /// <returns>The configured per-unit rate, or <c>0m</c> when absent.</returns>
    public decimal GetUnitRate(QuotaType dimension)
        => UnitRates.TryGetValue(dimension, out decimal rate) ? rate : 0m;
}
