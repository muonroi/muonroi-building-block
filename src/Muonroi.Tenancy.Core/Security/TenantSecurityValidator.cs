namespace Muonroi.Tenancy.Core.Security;

/// <summary>
/// Validates tenant identifiers across context, claims, and headers.
/// </summary>
public static class TenantSecurityValidator
{
    /// <summary>
    /// Error code for missing tenant context.
    /// </summary>
    public const string MissingTenantContext = "missing-tenant-context";
    /// <summary>
    /// Error code for missing tenant claim.
    /// </summary>
    public const string MissingTenantClaim = "missing-tenant-claim";
    /// <summary>
    /// Error code for context and claim mismatch.
    /// </summary>
    public const string TenantMismatch = "tenant-mismatch";
    /// <summary>
    /// Error code for header and claim mismatch.
    /// </summary>
    public const string HeaderClaimMismatch = "header-claim-mismatch";

    /// <summary>
    /// Validates tenant identifiers and returns a failure code if invalid.
    /// </summary>
    /// <param name="contextTenantId">Tenant identifier from context.</param>
    /// <param name="claimTenantId">Tenant identifier from claims.</param>
    /// <param name="headerTenantId">Tenant identifier from headers.</param>
    /// <param name="requireTenantClaim">Whether a claim is required.</param>
    /// <param name="errorCode">The failure code when validation fails.</param>
    /// <returns><c>true</c> if validation succeeds; otherwise, <c>false</c>.</returns>
    public static bool TryValidate(
        string? contextTenantId,
        string? claimTenantId,
        string? headerTenantId,
        bool requireTenantClaim,
        out string? errorCode)
    {
        string? normalizedContext = Normalize(contextTenantId);
        string? normalizedClaim = Normalize(claimTenantId);
        string? normalizedHeader = Normalize(headerTenantId);

        if (string.IsNullOrWhiteSpace(normalizedContext))
        {
            errorCode = MissingTenantContext;
            return false;
        }

        if (requireTenantClaim && string.IsNullOrWhiteSpace(normalizedClaim))
        {
            errorCode = MissingTenantClaim;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(normalizedClaim) &&
            !string.Equals(normalizedContext, normalizedClaim, StringComparison.Ordinal))
        {
            errorCode = TenantMismatch;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(normalizedClaim) &&
            !string.IsNullOrWhiteSpace(normalizedHeader) &&
            !string.Equals(normalizedClaim, normalizedHeader, StringComparison.Ordinal))
        {
            errorCode = HeaderClaimMismatch;
            return false;
        }

        errorCode = null;
        return true;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
