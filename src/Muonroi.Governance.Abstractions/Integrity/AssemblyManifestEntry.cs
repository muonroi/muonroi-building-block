namespace Muonroi.Governance.Abstractions.Integrity;

/// <summary>
/// Identifies a Muonroi assembly and its activation-time hash.
/// </summary>
public sealed record AssemblyManifestEntry
{
    public string AssemblyName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string Sha256Hash { get; init; } = string.Empty;

    public string? PublicKeyToken { get; init; }
}
