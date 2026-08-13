using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Dmn;

/// <summary>
/// Bidirectional mapping between Muonroi <see cref="HitPolicy"/> enum values
/// and DMN 1.3 hit policy / aggregation attribute strings.
/// </summary>
public static class DmnHitPolicyMapper
{
    /// <summary>
    /// Converts a Muonroi <see cref="HitPolicy"/> to a DMN hit policy string
    /// and an optional aggregation string.
    /// </summary>
    /// <param name="policy">The Muonroi hit policy enum value.</param>
    /// <returns>
    /// A tuple of (hitPolicy, aggregation) where aggregation is <c>null</c>
    /// unless the policy is a Collect variant (SUM, MIN, MAX, COUNT).
    /// </returns>
    public static (string HitPolicy, string? Aggregation) ToDmnString(HitPolicy policy) =>
        policy switch
        {
            HitPolicy.First        => ("FIRST",        null),
            HitPolicy.Unique       => ("UNIQUE",       null),
            HitPolicy.Collect      => ("COLLECT",      null),
            HitPolicy.Priority     => ("PRIORITY",     null),
            HitPolicy.OutputOrder  => ("OUTPUT ORDER", null),
            HitPolicy.CollectSum   => ("COLLECT",      "SUM"),
            HitPolicy.CollectMin   => ("COLLECT",      "MIN"),
            HitPolicy.CollectMax   => ("COLLECT",      "MAX"),
            HitPolicy.CollectCount => ("COLLECT",      "COUNT"),
            _                      => ("UNIQUE",       null)   // fallback — DMN spec default
        };

    /// <summary>
    /// Converts DMN hit policy and aggregation attribute strings back to a
    /// Muonroi <see cref="HitPolicy"/> value.
    /// </summary>
    /// <param name="hitPolicy">The DMN hitPolicy attribute value (case-insensitive).</param>
    /// <param name="aggregation">The DMN aggregation attribute value, or <c>null</c>.</param>
    /// <returns>
    /// The corresponding <see cref="HitPolicy"/>. Returns <see cref="HitPolicy.Unique"/>
    /// for any unrecognized value (DMN spec default).
    /// </returns>
    public static HitPolicy FromDmnString(string hitPolicy, string? aggregation)
    {
        string policy = hitPolicy?.ToUpperInvariant() ?? string.Empty;
        string? agg   = aggregation?.ToUpperInvariant();

        return policy switch
        {
            "FIRST"        => HitPolicy.First,
            "PRIORITY"     => HitPolicy.Priority,
            "OUTPUT ORDER" => HitPolicy.OutputOrder,
            "COLLECT" => agg switch
            {
                "SUM"   => HitPolicy.CollectSum,
                "MIN"   => HitPolicy.CollectMin,
                "MAX"   => HitPolicy.CollectMax,
                "COUNT" => HitPolicy.CollectCount,
                _       => HitPolicy.Collect
            },
            "UNIQUE" => HitPolicy.Unique,
            _        => HitPolicy.Unique   // DMN spec default for unknown values
        };
    }
}
