// Stub file â€” InjectAssemblyHash.ps1 hardcodes this path to inject the ExpectedHash constant.
// Deep generalization of CodeIntegrityVerifier to Pdf/Enterprise assemblies is deferred to Phase 8/Enterprise.
// See OSS-BOUNDARY.md and 07-05-SUMMARY.md for context.

namespace Muonroi.BuildingBlock.Shared.License;

/// <summary>
/// Placeholder for assembly hash injection target used by InjectAssemblyHash.ps1.
/// The actual integrity verification runtime is in Muonroi.Governance.Enterprise.
/// </summary>
internal static class CodeIntegrityVerifier
{
    private const string ExpectedHash = "4zpHQQ6k9eCiz4kpR3eMU4u/X6WTTylnLsgrugDkK6w=";
}
