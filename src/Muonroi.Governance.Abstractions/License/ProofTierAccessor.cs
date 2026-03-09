namespace Muonroi.Governance.License;

public sealed class ProofTierAccessor(LicenseState state, LicenseRuntimeStatus runtimeStatus)
{
    private readonly LicenseState _state = state ?? throw new ArgumentNullException(nameof(state));
    private readonly LicenseRuntimeStatus _runtimeStatus = runtimeStatus ?? throw new ArgumentNullException(nameof(runtimeStatus));

    public LicenseTier GetEffectiveTier()
    {
        return _runtimeStatus.GetEffectiveTier(_state);
    }

    public bool HasFeature(string featureName)
    {
        return _runtimeStatus.HasFeature(_state, featureName);
    }

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
