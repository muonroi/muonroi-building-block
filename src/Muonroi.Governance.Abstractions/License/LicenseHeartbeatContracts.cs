namespace Muonroi.Governance.License;

/// <summary>
/// Represents the License Heartbeat Request.
/// </summary>
public sealed class LicenseHeartbeatRequest
{
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Proof Id.
    /// </summary>
    public string ProofId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Machine Fingerprint.
    /// </summary>
    public string MachineFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Nonce.
    /// </summary>
    public string Nonce { get; set; } = string.Empty;
}

/// <summary>
/// Represents the License Heartbeat Response.
/// </summary>
public sealed class LicenseHeartbeatResponse
{
    /// <summary>
    /// Gets or sets the Success.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the Is Revoked.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Gets or sets the Error.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the New Nonce.
    /// </summary>
    public string? NewNonce { get; set; }

    /// <summary>
    /// Gets or sets the Checked At Utc.
    /// </summary>
    public DateTimeOffset CheckedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the Grace Until Utc.
    /// </summary>
    public DateTimeOffset? GraceUntilUtc { get; set; }
}
