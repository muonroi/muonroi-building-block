namespace Muonroi.Pdf.Enterprise;

/// <summary>
/// No-op <see cref="IFeatureGate"/> for unit tests and explicit OSS/dev opt-out only.
/// Every capability key is considered licensed; <see cref="EnsureFeatureOrThrow"/> never throws.
/// <para>
/// <b>This type is NEVER registered in production DI.</b> The real, fail-closed binding is
/// <see cref="License.LicenseFeatureGate"/>, wired by <c>AddPdfEnterprise()</c> via
/// <c>TryAddSingleton&lt;IFeatureGate, LicenseFeatureGate&gt;</c> (Phase 16, D-01). The license
/// gate delegates to <c>ILicenseGuard.HasFeature</c>, which reads the RSA-verified
/// <c>ActivationProof.Features[]</c> — RSA verification is the governance layer's responsibility,
/// not this gate's. Construct this stub directly (<see cref="Instance"/>) only in tests.
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
