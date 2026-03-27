using Muonroi.Governance.Abstractions.Integrity;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.License;

/// <summary>
/// Verifies that loaded Muonroi assemblies still match the activation proof manifest.
/// </summary>
public sealed class CodeIntegrityVerifier(
    IAssemblyHashCollector assemblyHashCollector,
    IMLog<CodeIntegrityVerifier>? logger = null)
{
    private readonly IAssemblyHashCollector _assemblyHashCollector =
        assemblyHashCollector ?? throw new ArgumentNullException(nameof(assemblyHashCollector));

    /// <summary>
    /// Executes the Verify Integrity operation.
    /// </summary>
    public bool VerifyIntegrity(LicenseState state, bool throwOnFailure = false)
    {
        ArgumentNullException.ThrowIfNull(state);

        ActivationProof? proof = state.ActivationProof;
        if (proof == null || proof.AllowedAssemblyHashes.Length == 0)
        {
            logger?.Info("[License] Activation proof has no assembly manifest. Integrity verification skipped.");
            return true;
        }

        IReadOnlyList<AssemblyManifestEntry> runtimeManifest = _assemblyHashCollector.Collect();
        Dictionary<string, AssemblyManifestEntry> approved = proof.AllowedAssemblyHashes.ToDictionary(
            x => $"{x.AssemblyName}|{x.Version}",
            x => x,
            StringComparer.OrdinalIgnoreCase);

        List<string> mismatches = [];
        foreach (AssemblyManifestEntry runtimeEntry in runtimeManifest)
        {
            string key = $"{runtimeEntry.AssemblyName}|{runtimeEntry.Version}";
            if (!approved.TryGetValue(key, out AssemblyManifestEntry? expected))
            {
                mismatches.Add($"{runtimeEntry.AssemblyName}@{runtimeEntry.Version}: not approved");
                continue;
            }

            if (!string.Equals(expected.Sha256Hash, runtimeEntry.Sha256Hash, StringComparison.Ordinal) ||
                !string.Equals(expected.PublicKeyToken ?? string.Empty, runtimeEntry.PublicKeyToken ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add($"{runtimeEntry.AssemblyName}@{runtimeEntry.Version}: hash mismatch");
            }
        }

        if (mismatches.Count == 0)
        {
            return true;
        }

        string message = "[SEC_INTEGRITY] Assembly integrity validation failed: " + string.Join(", ", mismatches);
        logger?.Warn(message);
        if (throwOnFailure)
        {
            throw new SecurityException(message);
        }

        return false;
    }
}
