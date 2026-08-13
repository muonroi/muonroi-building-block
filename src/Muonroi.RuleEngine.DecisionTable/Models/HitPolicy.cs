namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Determines how multiple matched rows are handled.
/// </summary>
public enum HitPolicy
{
    /// <summary>Take the first matched row.</summary>
    First,
    /// <summary>Require exactly one match.</summary>
    Unique,
    /// <summary>Collect all matched rows.</summary>
    Collect,
    /// <summary>Select match based on priority order.</summary>
    Priority,
    /// <summary>Collect outputs in their original order.</summary>
    OutputOrder,
    /// <summary>Aggregate numeric outputs by sum.</summary>
    CollectSum,
    /// <summary>Aggregate numeric outputs by minimum.</summary>
    CollectMin,
    /// <summary>Aggregate numeric outputs by maximum.</summary>
    CollectMax,
    /// <summary>Aggregate outputs by count.</summary>
    CollectCount
}
