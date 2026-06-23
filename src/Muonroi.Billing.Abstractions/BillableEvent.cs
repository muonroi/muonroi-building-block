namespace Muonroi.Billing.Abstractions;

/// <summary>
/// A single product-agnostic metered occurrence to be recorded for billing.
/// <para>
/// Keyed on <see cref="QuotaType"/> (the metered dimension), never on a product name such as
/// "pdf" — every product line (PDF, rule-engine, storyflow) bills through the same dimension key.
/// </para>
/// </summary>
/// <param name="TenantId">The tenant the event is attributed to.</param>
/// <param name="Dimension">The metered dimension this event counts against.</param>
/// <param name="Quantity">The number of metered units this event represents.</param>
/// <param name="OccurredAt">The instant the metered occurrence happened.</param>
public sealed record BillableEvent(
    string TenantId,
    QuotaType Dimension,
    long Quantity,
    DateTimeOffset OccurredAt);
