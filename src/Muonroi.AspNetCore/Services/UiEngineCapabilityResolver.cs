using Muonroi.Tenancy.Abstractions.Models;

namespace Muonroi.AspNetCore.Services;

public sealed class UiEngineCapabilityResolver(IEnumerable<IUiEngineManifestContributor> contributors)
{
    private readonly IReadOnlyList<IUiEngineManifestContributor> _contributors = [.. contributors];

    public List<MUiEngineCapability> BuildCapabilities(TenantTier tenantTier)
    {
        List<MUiEngineCapability> capabilities = [];
        Dictionary<string, TenantTier> moduleTiers = new(StringComparer.OrdinalIgnoreCase);

        foreach (IUiEngineManifestContributor contributor in _contributors)
        {
            if (string.IsNullOrWhiteSpace(contributor.ModuleId))
            {
                continue;
            }

            TenantTier requiredTier = ParseTier(contributor.RequiredTier);
            if (!moduleTiers.TryGetValue(contributor.ModuleId, out TenantTier existing) || requiredTier > existing)
            {
                moduleTiers[contributor.ModuleId] = requiredTier;
            }
        }

        foreach ((string moduleId, TenantTier requiredTier) in moduleTiers
            .Where(x => !string.Equals(x.Key, "rule-engine", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            capabilities.Add(new MUiEngineCapability
            {
                CapabilityKey = moduleId,
                DisplayName = ToDisplayName(moduleId),
                RequiredTier = requiredTier.ToString(),
                IsEnabled = tenantTier >= requiredTier
            });
        }

        if (moduleTiers.Count > 0)
        {
            TenantTier ruleEngineTier = moduleTiers.TryGetValue("rule-engine", out TenantTier t) ? t : moduleTiers.Values.Min();
            capabilities.Insert(0, new MUiEngineCapability
            {
                CapabilityKey = "rule-engine",
                DisplayName = "Rule Engine",
                RequiredTier = ruleEngineTier.ToString(),
                IsEnabled = tenantTier >= ruleEngineTier
            });
        }

        return capabilities;
    }

    public static TenantTier ParseTier(string? value)
    {
        if (Enum.TryParse(value, true, out TenantTier tier))
        {
            return tier;
        }

        return TenantTier.Free;
    }

    private static string ToDisplayName(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return "Module";
        }

        string normalized = moduleId.Replace("-", " ", StringComparison.Ordinal).Trim();
        if (normalized.Length == 0)
        {
            return "Module";
        }

        return string.Join(' ', normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant()));
    }
}
