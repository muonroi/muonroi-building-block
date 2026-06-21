namespace Muonroi.Billing.Abstractions;

/// <summary>
/// Service registration helpers for the billing seam.
/// </summary>
public static class BillingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the record-only default <see cref="IBillingProvider"/> (<see cref="RecordOnlyBillingProvider"/>).
    /// <para>
    /// Uses <c>TryAddSingleton</c> so a future payment-processor adapter can override the seam by
    /// registering its own <see cref="IBillingProvider"/> before this call (D-02).
    /// </para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddRecordOnlyBilling(this IServiceCollection services)
    {
        services.TryAddSingleton<IBillingProvider, RecordOnlyBillingProvider>();
        return services;
    }
}
