namespace Muonroi.Billing.Abstractions;

/// <summary>
/// Service registration helpers for the billing seam.
/// </summary>
public static class BillingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the record-only billing rail: the default <see cref="IBillingProvider"/>
    /// (<see cref="RecordOnlyBillingProvider"/>) and the default <see cref="IUsageAggregator"/>
    /// (<see cref="UsageAggregator"/>).
    /// <para>
    /// Both use <c>TryAddSingleton</c> so a future payment-processor adapter (or a custom
    /// aggregator) can override either seam by registering its own implementation before this
    /// call (D-02). 17-03 calls only this method and gets both seams wired.
    /// </para>
    /// <para>
    /// <see cref="UsageAggregator"/> depends on an <c>ITenantQuotaStore</c>; the host is
    /// responsible for registering that store (this method does not register it).
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddRecordOnlyBilling(this IServiceCollection services)
    {
        services.TryAddSingleton<IBillingProvider, RecordOnlyBillingProvider>();
        services.TryAddSingleton<IUsageAggregator, UsageAggregator>();
        return services;
    }
}
