using Muonroi.Governance.Abstractions.Integrity;

namespace Muonroi.Governance.License;

/// <summary>
/// Represents a signed activation proof from the Muonroi license server.
/// </summary>
public sealed class ActivationProof
{
    public string ProofId { get; set; } = string.Empty;

    public string LicenseKey { get; set; } = string.Empty;

    public string LicenseId { get; set; } = string.Empty;

    public string OrganizationName { get; set; } = string.Empty;

    public LicenseTier Tier { get; set; }

    public DateTimeOffset ActivatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public string ActivatedEnvironment { get; set; } = string.Empty;

    public int MaxSeats { get; set; }

    public string[] Features { get; set; } = [];

    public string? MachineFingerprint { get; set; }

    public string? ProductVersion { get; set; }

    public LicensePayload? SignedLicensePayload { get; set; }

    public string? SigningKeyId { get; set; }

    public AssemblyManifestEntry[] AllowedAssemblyHashes { get; set; } = [];

    public string? HeartbeatNonce { get; set; }

    public string Signature { get; set; } = string.Empty;

    public string GetSigningData()
    {
        string manifest = string.Join(";",
            AllowedAssemblyHashes
                .OrderBy(x => x.AssemblyName, StringComparer.Ordinal)
                .ThenBy(x => x.Version, StringComparer.Ordinal)
                .Select(x =>
                    $"{x.AssemblyName}:{x.Version}:{x.Sha256Hash}:{x.PublicKeyToken ?? string.Empty}"));

        return $"{GetLegacySigningData()}|{manifest}|{HeartbeatNonce ?? string.Empty}";
    }

    public string GetLegacySigningData()
    {
        return $"{ProofId}|{LicenseId}|{OrganizationName}|{Tier}|" +
               $"{ActivatedAt:O}|{ExpiresAt:O}|{ActivatedEnvironment}|" +
               $"{MaxSeats}|{string.Join(",", Features ?? [])}";
    }
}

public sealed class ActivationRequest
{
    public string LicenseKey { get; set; } = string.Empty;

    public string MachineFingerprint { get; set; } = string.Empty;

    public string ProductVersion { get; set; } = string.Empty;

    public DateTimeOffset ActivationTime { get; set; }

    public string Environment { get; set; } = string.Empty;

    public IReadOnlyList<AssemblyManifestEntry> AssemblyManifest { get; set; } = [];
}

public sealed class ActivationResponse
{
    public bool Success { get; set; }

    public string? Error { get; set; }

    public ActivationProof? Proof { get; set; }

    public string? ActivationProof { get; set; }

    /// <summary>
    /// RS256 JWT for frontend license verification (MLicenseVerifier).
    /// Contains claims: tier, features, license_id, organization_name, exp.
    /// </summary>
    public string? ActivationJwt { get; set; }

    public string? Message { get; set; }
}
