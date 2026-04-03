using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Governance.License;

/// <summary>
/// Represents the Proof Tier Accessor.
/// </summary>
public sealed class ProofTierAccessor(LicenseState state, LicenseRuntimeStatus runtimeStatus)
{
    private readonly LicenseState _state = MGuard.NotNull(state);
    private readonly LicenseRuntimeStatus _runtimeStatus = MGuard.NotNull(runtimeStatus);

    /// <summary>
    /// Executes the Get Effective Tier operation.
    /// </summary>
    public LicenseTier GetEffectiveTier()
    {
        return _runtimeStatus.GetEffectiveTier(_state);
    }

    /// <summary>
    /// Executes the Has Feature operation.
    /// </summary>
    public bool HasFeature(string featureName)
    {
        return _runtimeStatus.HasFeature(_state, featureName);
    }

    /// <summary>
    /// Executes the Require Minimum Tier operation.
    /// </summary>
    public void RequireMinimumTier(LicenseTier minimumTier, string featureName)
    {
        LicenseTier effectiveTier = GetEffectiveTier();
        if (effectiveTier >= minimumTier)
        {
            return;
        }

        throw new LicenseException(
            $"[LICENSE] Feature '{featureName}' requires tier '{minimumTier}'. Current effective tier is '{effectiveTier}'.");
    }
}
