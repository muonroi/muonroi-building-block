namespace Muonroi.Governance.Abstractions.Integrity;

/// <summary>
/// Identifies a Muonroi assembly and its activation-time hash.
/// </summary>
public sealed record AssemblyManifestEntry
{
    /// <summary>
    /// Gets or sets the Assembly Name.
    /// </summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the Version.
    /// </summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the Sha256 Hash.
    /// </summary>
    public string Sha256Hash { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the Public Key Token.
    /// </summary>
    public string? PublicKeyToken { get; init; }
}
