using Muonroi.Governance.License;

namespace Muonroi.Pdf.Enterprise.License;

/// <summary>
/// Real <see cref="IFeatureGate"/> bound to the shared Muonroi license guard.
/// Delegates to <see cref="ILicenseGuard.HasFeature"/> which reads
/// <c>ActivationProof.Features[]</c> via <c>LicenseCapabilityResolver.HasAccess</c>.
/// </summary>
/// <remarks>
/// Do NOT call <c>ILicenseGuard.EnsureFeature</c> — that throws <c>MInternalException</c>
/// (governance-specific boundary). Use <see cref="ILicenseGuard.HasFeature"/> and throw
/// the PDF-specific <see cref="FeatureNotLicensedException"/> to preserve the clean boundary.
/// </remarks>
public sealed class LicenseFeatureGate(ILicenseGuard licenseGuard) : IFeatureGate
{
    /// <inheritdoc/>
    public bool IsEnabled(string capabilityKey)
        => licenseGuard.HasFeature(capabilityKey);

    /// <inheritdoc/>
    public void EnsureFeatureOrThrow(string capabilityKey)
    {
        if (!licenseGuard.HasFeature(capabilityKey))
        {
            // MSTD0001: FeatureNotLicensedException inherits InvalidOperationException (not MException)
            // by design — callers must be able to catch it without a Muonroi.Governance dependency.
#pragma warning disable MSTD0001
            throw new FeatureNotLicensedException(capabilityKey);
#pragma warning restore MSTD0001
        }
    }
}
