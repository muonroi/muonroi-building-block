using Muonroi.Governance.Abstractions.Integrity;

namespace Muonroi.Governance.License;

/// <summary>
/// Represents a signed activation proof from the Muonroi license server.
/// </summary>
public sealed class ActivationProof
{
    /// <summary>Activation proof identifier.</summary>
    public string ProofId { get; set; } = string.Empty;

    /// <summary>License key used during activation.</summary>
    public string LicenseKey { get; set; } = string.Empty;

    /// <summary>License identifier.</summary>
    public string LicenseId { get; set; } = string.Empty;

    /// <summary>Organization name associated with the license.</summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>Licensed tier.</summary>
    public LicenseTier Tier { get; set; }

    /// <summary>Activation timestamp.</summary>
    public DateTimeOffset ActivatedAt { get; set; }

    /// <summary>Expiration timestamp.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Activation environment identifier.</summary>
    public string ActivatedEnvironment { get; set; } = string.Empty;

    /// <summary>Maximum licensed seats.</summary>
    public int MaxSeats { get; set; }

    /// <summary>Enabled feature keys.</summary>
    public string[] Features { get; set; } = [];

    /// <summary>Hardware fingerprint captured at activation time.</summary>
    public string? MachineFingerprint { get; set; }

    /// <summary>Product version at activation time.</summary>
    public string? ProductVersion { get; set; }

    /// <summary>Signed license payload.</summary>
    public LicensePayload? SignedLicensePayload { get; set; }

    /// <summary>Signing key identifier.</summary>
    public string? SigningKeyId { get; set; }

    /// <summary>Allowed assembly hash whitelist.</summary>
    public AssemblyManifestEntry[] AllowedAssemblyHashes { get; set; } = [];

    /// <summary>Heartbeat nonce for online validation.</summary>
    public string? HeartbeatNonce { get; set; }

    /// <summary>Signature of the activation proof payload.</summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>Builds the signing payload including manifest and nonce.</summary>
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

    /// <summary>Builds the legacy signing payload without manifest data.</summary>
    public string GetLegacySigningData()
    {
        return $"{ProofId}|{LicenseId}|{OrganizationName}|{Tier}|" +
               $"{ActivatedAt:O}|{ExpiresAt:O}|{ActivatedEnvironment}|" +
               $"{MaxSeats}|{string.Join(",", Features ?? [])}";
    }
}

/// <summary>
/// Represents the activation request payload.
/// </summary>
public sealed class ActivationRequest
{
    /// <summary>License key to activate.</summary>
    public string LicenseKey { get; set; } = string.Empty;

    /// <summary>Machine fingerprint for binding the license.</summary>
    public string MachineFingerprint { get; set; } = string.Empty;

    /// <summary>Product version at activation time.</summary>
    public string ProductVersion { get; set; } = string.Empty;

    /// <summary>Activation timestamp.</summary>
    public DateTimeOffset ActivationTime { get; set; }

    /// <summary>Environment identifier.</summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>Assembly manifest included in the request.</summary>
    public IReadOnlyList<AssemblyManifestEntry> AssemblyManifest { get; set; } = [];
}

/// <summary>
/// Represents the activation response payload.
/// </summary>
public sealed class ActivationResponse
{
    /// <summary>True when activation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Optional error message.</summary>
    public string? Error { get; set; }

    /// <summary>Activation proof details.</summary>
    public ActivationProof? Proof { get; set; }

    /// <summary>Serialized activation proof (for storage).</summary>
    public string? ActivationProof { get; set; }

    /// <summary>
    /// RS256 JWT for frontend license verification (MLicenseVerifier).
    /// Contains claims: tier, features, license_id, organization_name, exp.
    /// </summary>
    public string? ActivationJwt { get; set; }

    /// <summary>Optional informational message.</summary>
    public string? Message { get; set; }
}
