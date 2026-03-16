namespace Muonroi.Governance.License;

/// <summary>
/// Canonical mapping between premium feature keys and the minimum required license tier.
/// </summary>
public static class FeatureTierMap
{
    private static readonly IReadOnlyDictionary<string, LicenseTier> MinimumTierByFeature =
        new Dictionary<string, LicenseTier>(StringComparer.OrdinalIgnoreCase)
        {
            [FreeTierFeatures.Premium.MultiTenant] = LicenseTier.Licensed,
            [FreeTierFeatures.Premium.AdvancedAuth] = LicenseTier.Licensed,
            [FreeTierFeatures.Premium.RuleEngine] = LicenseTier.Licensed,
            [FreeTierFeatures.Premium.Grpc] = LicenseTier.Licensed,
            [FreeTierFeatures.Premium.MessageBus] = LicenseTier.Licensed,
            [FreeTierFeatures.Premium.DistributedCache] = LicenseTier.Licensed,
            [FreeTierFeatures.Premium.AuditTrail] = LicenseTier.Licensed,
            [FreeTierFeatures.Premium.AntiTampering] = LicenseTier.Licensed,
            [FreeTierFeatures.Premium.Connectors] = LicenseTier.Licensed,
            [FreeTierFeatures.Premium.JavaScriptExpressions] = LicenseTier.Licensed
        };

    public static bool TryGetMinimumTier(string featureName, out LicenseTier minimumTier)
    {
        if (string.IsNullOrWhiteSpace(featureName))
        {
            minimumTier = LicenseTier.Free;
            return false;
        }

        return MinimumTierByFeature.TryGetValue(featureName, out minimumTier);
    }
}
