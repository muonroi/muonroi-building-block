namespace Muonroi.RuleEngine.DecisionTable.Models;

public enum HitPolicy
{
    First,
    Unique,
    Collect,
    Priority,
    OutputOrder,
    CollectSum,
    CollectMin,
    CollectMax,
    CollectCount
}
