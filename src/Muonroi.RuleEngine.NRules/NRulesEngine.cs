using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleEngine.NRules;

/// <summary>
/// Executes rules defined using NRules and supports rule versioning and toggling.
/// </summary>
[Obsolete("Frozen: Use Muonroi.RuleEngine.Runtime instead. NRules integration is no longer actively developed.")]
public sealed class NRulesEngine
{
    private readonly ISessionFactory _sessionFactory;

    /// <summary>
    /// Initialises the engine by scanning <paramref name="assemblies"/> for NRules rule types,
    /// filtering them according to <paramref name="options"/>, checking for name conflicts,
    /// and compiling the enabled rules into a reusable session factory.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for NRules rule class definitions.</param>
    /// <param name="options">Configuration that controls which rules are enabled and their expected versions.</param>
    public NRulesEngine(IEnumerable<Assembly> assemblies, IOptions<RuleOptions> options)
    {
        RuleRepository repository = new();
        repository.Load(x => x.NestedTypes().From(assemblies));
        List<IRuleDefinition> all = [.. repository.GetRules()];
        List<IRuleDefinition> enabled = [.. all.Where(r => IsEnabled(r, options.Value))];
        DetectConflicts(enabled);
        RuleCompiler compiler = new();
        _sessionFactory = compiler.Compile(enabled);
    }

    /// <summary>Inserts facts and fires the rules.</summary>
    public void Fire(params object[] facts)
    {
        ISession session = _sessionFactory.CreateSession();
        foreach (object fact in facts)
        {
            session.Insert(fact);
        }

        session.Fire();
    }

    private static bool IsEnabled(IRuleDefinition rule, RuleOptions options)
    {
        Type? ruleType = GetRuleType(rule);
        RuleAttribute? attr = ruleType?.GetCustomAttribute<RuleAttribute>();
        string name = attr?.Name ?? rule.Name;
        if (options.Rules.TryGetValue(name, out RuleConfig? cfg))
        {
            if (!cfg.Enabled)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(cfg.Version) && attr != null &&
                !string.Equals(cfg.Version, attr.Version, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static void DetectConflicts(IEnumerable<IRuleDefinition> rules)
    {
        List<IGrouping<string, IRuleDefinition>> conflicts = [.. rules.GroupBy(GetName).Where(g => g.Count() > 1)];
        if (conflicts.Count > 0)
        {
            string names = string.Join(", ", conflicts.Select(g => g.Key));
            throw new MInternalException($"Conflicting rules detected: {names}");
        }
    }

    private static string GetName(IRuleDefinition rule)
    {
        Type? ruleType = GetRuleType(rule);
        RuleAttribute? attr = ruleType?.GetCustomAttribute<RuleAttribute>();
        return attr?.Name ?? rule.Name;
    }

    private static Type? GetRuleType(IRuleDefinition rule)
    {
        if (rule.Properties.TryGetProperty(RuleProperties.ClrType, out RuleProperty? property))
        {
            return property.Value as Type;
        }

        return null;
    }
}
