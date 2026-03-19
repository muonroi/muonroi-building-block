using Muonroi.Governance.License;

namespace Muonroi.Governance.Abstractions.License;

public static class LicenseCapabilityResolver
{
    public static class Capabilities
    {
        public const string CoreRuntime = "core.runtime";
        public const string AuthRbacPlus = "auth.rbac_plus";
        public const string TenancyStrict = "tenancy.strict";
        public const string RulesRuntime = "rules.runtime";
        public const string TransportGrpc = "transport.grpc";
        public const string TransportMessageBus = "transport.message_bus";
        public const string CacheDistributed = "cache.distributed";
        public const string AuditTrail = "audit.trail";
        public const string RuntimeAntiTampering = "runtime.anti_tampering";
        public const string AuditRemote = "audit.remote";
        public const string Connectors = "connectors";
        public const string JavaScriptExpressions = "js_expressions";
    }

    private static readonly Dictionary<string, string> FeatureToCapability =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [FreeTierFeatures.Premium.AdvancedAuth] = Capabilities.AuthRbacPlus,
            [FreeTierFeatures.Premium.MultiTenant] = Capabilities.TenancyStrict,
            [FreeTierFeatures.Premium.RuleEngine] = Capabilities.RulesRuntime,
            [FreeTierFeatures.Premium.Grpc] = Capabilities.TransportGrpc,
            [FreeTierFeatures.Premium.MessageBus] = Capabilities.TransportMessageBus,
            [FreeTierFeatures.Premium.DistributedCache] = Capabilities.CacheDistributed,
            [FreeTierFeatures.Premium.AuditTrail] = Capabilities.AuditTrail,
            [FreeTierFeatures.Premium.AntiTampering] = Capabilities.RuntimeAntiTampering,
            ["server-validation"] = Capabilities.AuditRemote,
            [FreeTierFeatures.Premium.Connectors] = Capabilities.Connectors,
            [FreeTierFeatures.Premium.JavaScriptExpressions] = Capabilities.JavaScriptExpressions
        };

    private static readonly HashSet<string> CapabilityKeys =
    [
        Capabilities.CoreRuntime,
        Capabilities.AuthRbacPlus,
        Capabilities.TenancyStrict,
        Capabilities.RulesRuntime,
        Capabilities.TransportGrpc,
        Capabilities.TransportMessageBus,
        Capabilities.CacheDistributed,
        Capabilities.AuditTrail,
        Capabilities.RuntimeAntiTampering,
        Capabilities.AuditRemote,
        Capabilities.Connectors,
        Capabilities.JavaScriptExpressions
    ];

    public static bool HasAccess(LicenseState state, string requestedFeature)
    {
        if (string.IsNullOrWhiteSpace(requestedFeature))
        {
            return false;
        }

        string requested = Normalize(requestedFeature);

        // Invalid/tampered paid licenses are restricted to the Free baseline only.
        if (!state.IsValid)
        {
            return IsFreeBaseline(requested);
        }

        // All valid tiers keep Free baseline.
        if (IsFreeBaseline(requested))
        {
            return true;
        }

        if (state.Tier == LicenseTier.Enterprise)
        {
            return true;
        }

        // E0 unification: paid tiers get a capability-driven core runtime profile.
        // This removes the need to manually list every api./db./http. action key.
        if (state.Tier != LicenseTier.Free &&
            (IsCoreRuntimeAction(requested) ||
             requested.Equals(Capabilities.CoreRuntime, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (HasWildcard(state.Features) || HasWildcard(state.Payload?.AllowedFeatures))
        {
            return true;
        }

        if (ContainsEntitlement(state.Features, requested) ||
            ContainsEntitlement(state.Payload?.AllowedFeatures, requested))
        {
            return true;
        }

        string? requestedCapability = ResolveCapability(requested);
        if (requestedCapability is null)
        {
            return false;
        }

        return HasCapability(state.Features, requestedCapability) ||
               HasCapability(state.Payload?.AllowedFeatures, requestedCapability);
    }

    public static string? ResolveCapabilityForRequest(string requestedFeature)
    {
        if (string.IsNullOrWhiteSpace(requestedFeature))
        {
            return null;
        }

        return ResolveCapability(Normalize(requestedFeature));
    }

    private static string Normalize(string key)
    {
        return key.Trim().ToLowerInvariant();
    }

    private static bool IsFreeBaseline(string key)
    {
        return FreeTierFeatures.IsAllowed(key);
    }

    private static bool HasWildcard(string[]? entries)
    {
        if (entries == null || entries.Length == 0)
        {
            return false;
        }

        foreach (string item in entries)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            if (Normalize(item) == "*")
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsEntitlement(string[]? entries, string requested)
    {
        if (entries == null || entries.Length == 0)
        {
            return false;
        }

        foreach (string item in entries)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            if (Normalize(item).Equals(requested, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCapability(string[]? entries, string capability)
    {
        if (entries == null || entries.Length == 0)
        {
            return false;
        }

        foreach (string item in entries)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            string normalized = Normalize(item);
            if (normalized.Equals(capability, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (FeatureToCapability.TryGetValue(normalized, out string? mappedCapability) &&
                mappedCapability.Equals(capability, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveCapability(string requested)
    {
        if (CapabilityKeys.Contains(requested))
        {
            return requested;
        }

        if (FeatureToCapability.TryGetValue(requested, out string? capability))
        {
            return capability;
        }

        if (IsCoreRuntimeAction(requested))
        {
            return Capabilities.CoreRuntime;
        }

        return null;
    }

    private static bool IsCoreRuntimeAction(string requested)
    {
        return requested.StartsWith("api.", StringComparison.OrdinalIgnoreCase) ||
               requested.StartsWith("db.", StringComparison.OrdinalIgnoreCase) ||
               requested.StartsWith("http.", StringComparison.OrdinalIgnoreCase);
    }
}
