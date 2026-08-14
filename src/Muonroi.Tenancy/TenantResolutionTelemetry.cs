namespace Muonroi.Tenancy;

/// <summary>
/// OpenTelemetry metrics for tenant resolution failures.
/// Uses System.Diagnostics.Metrics (OTel-native in .NET 8).
/// </summary>
public static class TenantResolutionTelemetry
{
    /// <summary>Meter name following OTel conventions.</summary>
    public const string MeterName = "muonroi.tenancy";

    /// <summary>Counter instrument name.</summary>
    public const string AuthFailureCounterName = "muonroi.tenancy.auth_failures";

    private static readonly Meter TenancyMeter = new(MeterName, "1.0.0");

    /// <summary>
    /// Counter tracking cross-tenant authentication failures.
    /// Dimensions: failure_reason, header_tenant_id, claim_tenant_id.
    /// </summary>
    public static readonly Counter<long> AuthFailureCounter =
        TenancyMeter.CreateCounter<long>(
            AuthFailureCounterName,
            unit: "{failure}",
            description: "Number of tenant resolution authentication failures");

    /// <summary>
    /// Records a tenant auth failure with structured tags for alerting and correlation.
    /// </summary>
    /// <param name="failureReason">Reason code: "missing_claim" or "header_claim_mismatch".</param>
    /// <param name="headerTenantId">Tenant ID from the request header (if present).</param>
    /// <param name="claimTenantId">Tenant ID from the JWT claim (if present).</param>
    public static void RecordAuthFailure(
        string failureReason,
        string? headerTenantId = null,
        string? claimTenantId = null)
    {
        AuthFailureCounter.Add(1,
            new KeyValuePair<string, object?>("failure_reason", failureReason),
            new KeyValuePair<string, object?>("header_tenant_id", headerTenantId ?? ""),
            new KeyValuePair<string, object?>("claim_tenant_id", claimTenantId ?? ""));
    }
}
