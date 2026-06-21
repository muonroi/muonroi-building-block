namespace Muonroi.Billing.Abstractions;

/// <summary>
/// The product-agnostic billing seam every product line bills through (MON-01, D-02).
/// <para>
/// The default implementation (<c>RecordOnlyBillingProvider</c>) is record-only: it records
/// billable events and computes invoice previews without calling any external service. The
/// payment-processor (e.g. Stripe) adapter is a deferred, separate implementation behind this seam
/// and is NOT a build/test dependency of this package.
/// </para>
/// </summary>
public interface IBillingProvider
{
    /// <summary>
    /// Records a metered billable event.
    /// <para>
    /// In the default record-only implementation this performs no external/network call; the event
    /// is captured locally. Recording must never throw to the caller — a sink failure is logged with
    /// context and swallowed (D-02: record-only, never blocks; No Silent Catch).
    /// </para>
    /// </summary>
    /// <param name="billableEvent">The metered occurrence to record.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task that completes when the event has been recorded (or the failure logged).</returns>
    Task RecordAsync(BillableEvent billableEvent, CancellationToken ct = default);

    /// <summary>
    /// Computes an invoice preview for the supplied priced line items (D-03).
    /// <para>
    /// This is compute-only and never charges: in the default record-only implementation it returns
    /// the supplied line items unchanged and performs no external call. The payment-processor adapter
    /// is a deferred, separate implementation behind this seam.
    /// </para>
    /// </summary>
    /// <param name="tenantId">The tenant the preview is computed for.</param>
    /// <param name="lineItems">The priced line items to preview.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The previewed line items. No charge is executed.</returns>
    Task<IReadOnlyList<UsageLineItem>> PreviewInvoiceAsync(
        string tenantId,
        IReadOnlyList<UsageLineItem> lineItems,
        CancellationToken ct = default);
}
