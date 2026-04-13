namespace Muonroi.Core.Abstractions.Ecosystem;

/// <summary>
/// Defines the four ecosystem anchors for Muonroi capability detection.
/// Each bit represents one pillar of the ecosystem. Bits 4-15 are reserved
/// for consumer-defined extension capabilities.
/// </summary>
/// <remarks>
/// Designed to be AOT-safe (no reflection). Use bitwise operations for
/// combining and testing capabilities.
/// </remarks>
[Flags]
public enum MCapability
{
    /// <summary>No capabilities registered.</summary>
    None = 0,

    /// <summary>Structured logging via IMLog&lt;T&gt; is present in the ecosystem.</summary>
    Logging = 1 << 0,

    /// <summary>The rule engine (RuleOrchestrator, FactBag pipeline) is present in the ecosystem.</summary>
    RuleEngine = 1 << 1,

    /// <summary>Multi-tenancy (TenantContext, AsyncLocal propagation) is present in the ecosystem.</summary>
    MultiTenant = 1 << 2,

    /// <summary>Auth governance (license guard, HMAC chain, anti-tamper) is present in the ecosystem.</summary>
    Auth = 1 << 3,

    /// <summary>License governance (license guard integration with rule engine) is present in the ecosystem.</summary>
    Governance = 1 << 4,
}
