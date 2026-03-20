using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Governance.License;

/// <summary>
/// Represents the Hmac Fingerprint Signer.
/// </summary>
public sealed class HmacFingerprintSigner(LicensePayload? payload, LicenseConfigs configs)
    : IFingerprintSigner
{
    private readonly LicenseConfigs _configs = configs ?? throw new ArgumentNullException(nameof(configs));

    /// <summary>
    /// Executes the Compute Signature operation.
    /// </summary>
    public string ComputeSignature(string previousSignature, LicenseActionContext context, long sequence)
    {
        byte[] key = BuildKey(payload);
        string tenantPartition = AuditTrailTenantPartition.Normalize(context.TenantId);
        using HMACSHA256 hmac = new(key);
        string data =
            $"{previousSignature}|{sequence}|{tenantPartition}|{context.ActionType}|{context.ActionName}|{context.PayloadHash}|{context.Timestamp.UtcDateTime:O}";
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash);
    }

    private byte[] BuildKey(LicensePayload? licensePayload)
    {
        string seed = licensePayload?.Signature ?? licensePayload?.LicenseId ?? "GENESIS_CORE";
        string projectSeed = _configs.ProjectSeed ?? "COMMUNITY_DEFAULT";
        string salt = _configs.FingerprintSalt ?? string.Empty;

        // Critical: Use ServerNonce from the signed licensePayload. 
        // If the cracker fakes the licensePayload, they won't have the real nonce 
        // that matches the server's expected execution state.
        string serverNonce = licensePayload?.ServerNonce ?? "NO_SERVER_CONNECTION";

        // Combine all elements into a functional dependency chain
        string keySource = $"{seed}:{projectSeed}:{salt}:{serverNonce}";
        return Encoding.UTF8.GetBytes(keySource);
    }
}
