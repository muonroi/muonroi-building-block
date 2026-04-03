namespace Muonroi.Governance.License;

/// <summary>
/// Represents the Fingerprint Chain Entry.
/// </summary>
public sealed class FingerprintChainEntry
{
    /// <summary>
    /// Gets or sets the Sequence.
    /// </summary>
    public long Sequence { get; set; }
    /// <summary>
    /// Gets or sets the Timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Action Type.
    /// </summary>
    public string? ActionType { get; set; }
    /// <summary>
    /// Gets or sets the Action Name.
    /// </summary>
    public string? ActionName { get; set; }
    /// <summary>
    /// Gets or sets the Payload Hash.
    /// </summary>
    public string? PayloadHash { get; set; }
    /// <summary>
    /// Gets or sets the Previous Signature.
    /// </summary>
    public string? PreviousSignature { get; set; }
    /// <summary>
    /// Gets or sets the Signature.
    /// </summary>
    public string? Signature { get; set; }
}
