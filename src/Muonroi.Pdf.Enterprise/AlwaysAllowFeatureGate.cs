namespace Muonroi.Pdf.Enterprise;

/// <summary>
/// No-op <see cref="IFeatureGate"/> used in OSS / development scenarios.
/// Every capability key is considered licensed; <see cref="EnsureFeatureOrThrow"/> never throws.
/// <para>
/// Real binding to ActivationProof RSA verification lands in Phase 9.4.
/// </para>
/// </summary>
public sealed class AlwaysAllowFeatureGate : IFeatureGate
{
    /// <summary>Shared singleton — stateless, safe to reuse.</summary>
    public static readonly IFeatureGate Instance = new AlwaysAllowFeatureGate();

    private AlwaysAllowFeatureGate() { }

    /// <inheritdoc/>
    public bool IsEnabled(string capabilityKey) => true;

    /// <inheritdoc/>
    public void EnsureFeatureOrThrow(string capabilityKey) { /* no-op: all features allowed */ }
}
