namespace Muonroi.Pdf.Enterprise;

/// <summary>
/// Thrown when a capability key is not included in the current license.
/// Inherits <see cref="InvalidOperationException"/> so callers can catch without
/// taking a dependency on Muonroi.Governance.Abstractions.
/// </summary>
public sealed class FeatureNotLicensedException : InvalidOperationException
{
    /// <summary>The capability key that was denied.</summary>
    public string CapabilityKey { get; }

    public FeatureNotLicensedException(string capabilityKey)
        : base($"[PDF] Feature '{capabilityKey}' is not included in the current license.")
    {
        CapabilityKey = capabilityKey;
    }
}
