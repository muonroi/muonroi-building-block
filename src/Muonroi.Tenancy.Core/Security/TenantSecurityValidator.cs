namespace Muonroi.Tenancy.Core.Security;

public static class TenantSecurityValidator
{
    public const string MissingTenantContext = "missing-tenant-context";
    public const string MissingTenantClaim = "missing-tenant-claim";
    public const string TenantMismatch = "tenant-mismatch";
    public const string HeaderClaimMismatch = "header-claim-mismatch";

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
