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

    /// <summary>Initializes a new instance with the denied capability key.</summary>
    /// <param name="capabilityKey">The capability key that was denied by the license check.</param>
    public FeatureNotLicensedException(string capabilityKey)
        : base($"[PDF] Feature '{capabilityKey}' is not included in the current license.")
    {
        CapabilityKey = capabilityKey;
    }
}
