namespace Muonroi.Billing.Abstractions;

/// <summary>
/// The default record-only <see cref="IBillingProvider"/> (MON-02, D-02).
/// <para>
/// <see cref="RecordAsync"/> appends the event to an in-memory, thread-safe store and performs no
/// external/network call. A sink failure is logged with module/operation/context and swallowed so
/// recording never blocks the caller (T-17-01 mitigation; No Silent Catch). <see cref="PreviewInvoiceAsync"/>
/// is compute-only (D-03): it returns the supplied line items unchanged and never executes a charge.
/// The payment-processor (Stripe) adapter is a deferred, separate implementation behind this seam.
/// </para>
/// </summary>
public sealed class RecordOnlyBillingProvider : IBillingProvider
{
    private readonly ConcurrentBag<BillableEvent> _recordedEvents = [];
    private readonly IMLog<RecordOnlyBillingProvider>? _logger;
    private readonly Action<BillableEvent>? _sink;

    /// <summary>
    /// Initializes a new <see cref="RecordOnlyBillingProvider"/>.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record sink failures with context (No Silent Catch). When null,
    /// failures are swallowed silently after the recorded-events store remains consistent.
    /// </param>
    /// <param name="sink">
    /// An optional side-effect invoked for each recorded event (e.g. a downstream recording store).
    /// Defaults to no-op. A throwing sink is caught, logged, and swallowed.
    /// </param>
    public RecordOnlyBillingProvider(
        IMLog<RecordOnlyBillingProvider>? logger = null,
        Action<BillableEvent>? sink = null)
    {
        _logger = logger;
        _sink = sink;
    }

    /// <summary>
    /// Gets a snapshot of the billable events recorded by this provider. Observable by tests and
    /// callers; the default record-only provider never sends these anywhere external.
    /// </summary>
    public IReadOnlyList<BillableEvent> RecordedEvents => [.. _recordedEvents];

    /// <inheritdoc />
    public Task RecordAsync(BillableEvent billableEvent, CancellationToken ct = default)
    {
        try
        {
            _recordedEvents.Add(billableEvent);
            _sink?.Invoke(billableEvent);
        }
        catch (Exception ex)
        {
            // No Silent Catch (CLAUDE.md): log error with context, then swallow so recording never
            // blocks the caller (D-02: record-only, never blocks; T-17-01 DoS mitigation).
            _logger?.Error(
                ex,
                "[Billing] Record failed (non-blocking) for tenant {TenantId} dimension {Dimension}: {Message}",
                billableEvent.TenantId,
                billableEvent.Dimension,
                ex.Message);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UsageLineItem>> PreviewInvoiceAsync(
        string tenantId,
        IReadOnlyList<UsageLineItem> lineItems,
        CancellationToken ct = default)
    {
        // Compute-only (D-03): return the supplied line items verbatim. No external call, no charge.
        return Task.FromResult(lineItems);
    }
}
