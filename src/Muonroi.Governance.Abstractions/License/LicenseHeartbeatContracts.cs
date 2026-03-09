namespace Muonroi.Governance.License;

public sealed class LicenseHeartbeatRequest
{
    public string LicenseId { get; set; } = string.Empty;

    public string ProofId { get; set; } = string.Empty;

    public string MachineFingerprint { get; set; } = string.Empty;

    public string Nonce { get; set; } = string.Empty;
}

public sealed class LicenseHeartbeatResponse
{
    public bool Success { get; set; }

    public bool IsRevoked { get; set; }

    public string? Error { get; set; }

    public string? NewNonce { get; set; }

    public DateTimeOffset CheckedAtUtc { get; set; }

    public DateTimeOffset? GraceUntilUtc { get; set; }
}
