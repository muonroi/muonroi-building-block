namespace Muonroi.Governance.License;

/// <summary>
/// Represents a signed activation proof from the Muonroi license server.
/// This proof is generated during online activation (dev/staging/CI-CD) and can be verified offline in production.
///
/// WORKFLOW:
/// 1. Development/CI-CD (internet available): Activate online → receive signed proof
/// 2. Production (offline): Verify proof signature → no internet needed
///
/// This leverages the natural deployment pipeline where dev/staging has internet,
/// but production may be air-gapped or in corporate networks without internet access.
/// </summary>
public sealed class ActivationProof
{
    /// <summary>
    /// Unique identifier for this activation proof.
    /// </summary>
    public string ProofId { get; set; } = string.Empty;

    /// <summary>
    /// License key used for this activation.
    /// </summary>
    public string LicenseKey { get; set; } = string.Empty;

    /// <summary>
    /// License ID from the Muonroi license system.
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;

    /// <summary>
    /// Organization name (company/individual) this license belongs to.
    /// </summary>
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>
    /// License tier (Free, Licensed, Enterprise).
    /// </summary>
    public LicenseTier Tier { get; set; }

    /// <summary>
    /// When this proof was activated (UTC).
    /// </summary>
    public DateTimeOffset ActivatedAt { get; set; }

    /// <summary>
    /// When this license expires (UTC).
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Environment where activation occurred (Development, Staging, Production).
    /// </summary>
    public string ActivatedEnvironment { get; set; } = string.Empty;

    /// <summary>
    /// Maximum concurrent users/seats allowed.
    /// </summary>
    public int MaxSeats { get; set; }

    /// <summary>
    /// Enabled features for this license.
    /// </summary>
    public string[] Features { get; set; } = [];

    /// <summary>
    /// Machine fingerprint where activation occurred.
    /// Used for tracking but not enforced (allows deployment to different machines).
    /// </summary>
    public string? MachineFingerprint { get; set; }

    /// <summary>
    /// Product version at time of activation.
    /// </summary>
    public string? ProductVersion { get; set; }

    /// <summary>
    /// Signed license payload used for offline verification by LicenseVerifier.
    /// </summary>
    public LicensePayload? SignedLicensePayload { get; set; }

    /// <summary>
    /// Signing key identifier used to sign this proof.
    /// </summary>
    public string? SigningKeyId { get; set; }

    /// <summary>
    /// RSA-SHA256 signature of this activation proof.
    /// Signed by the Muonroi license server's private key.
    /// Can be verified offline using the public key.
    ///
    /// This is the CORE security mechanism - cannot be forged without the server's private key.
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Gets the signing data string used to generate/verify the signature.
    /// </summary>
    public string GetSigningData()
    {
        return $"{ProofId}|{LicenseId}|{OrganizationName}|{Tier}|" +
               $"{ActivatedAt:O}|{ExpiresAt:O}|{ActivatedEnvironment}|" +
               $"{MaxSeats}|{string.Join(",", Features ?? [])}";
    }
}

/// <summary>
/// Request to activate a license online.
/// Sent from client to Muonroi license server during activation.
/// </summary>
public sealed class ActivationRequest
{
    /// <summary>
    /// License key (from license.json or config).
    /// </summary>
    public string LicenseKey { get; set; } = string.Empty;

    /// <summary>
    /// Machine fingerprint (for tracking, not enforcement).
    /// </summary>
    public string MachineFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Product version requesting activation.
    /// </summary>
    public string ProductVersion { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of activation request (UTC).
    /// </summary>
    public DateTimeOffset ActivationTime { get; set; }

    /// <summary>
    /// Environment requesting activation (Development, Staging, Production).
    /// </summary>
    public string Environment { get; set; } = string.Empty;
}

/// <summary>
/// Response from license server after activation request.
/// </summary>
public sealed class ActivationResponse
{
    /// <summary>
    /// Whether activation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if activation failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// The signed activation proof (if successful).
    /// </summary>
    public ActivationProof? Proof { get; set; }

    /// <summary>
    /// Base64-encoded JSON activation proof content.
    /// </summary>
    public string? ActivationProof { get; set; }

    /// <summary>
    /// Additional information about the activation.
    /// </summary>
    public string? Message { get; set; }
}
