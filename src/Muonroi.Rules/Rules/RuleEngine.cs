using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Muonroi.Governance.License;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Core;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Muonroi.Rules.Rules;

/// <summary>
/// A simple rule engine that allows registering different rules and executing them
/// based on their <see cref="RuleType"/>. This helps separating validation logic
/// from business logic and lets callers decide which rules to run at runtime.
/// </summary>
/// <typeparam name="T">Type of context passed to each rule.</typeparam>
public sealed class RuleEngine<T>(
    IOptionsMonitor<RuleOptions>? options = null,
    IMLog<RuleEngine<T>>? logger = null,
    IRuleActivationStrategy<T>? activation = null,
    ILicenseGuard? licenseGuard = null)
{
    private readonly List<(IRule<T> Rule, RuleDescriptor Descriptor)> _rules = [];
    private readonly ConcurrentDictionary<string, CachedExecutionPlan> _executionPlanCache =
        new(StringComparer.OrdinalIgnoreCase);
    private int _rulesVersion;
    private const int MaxExecutionPlanCacheEntries = 1024;

    private static readonly Meter _meter = new("Muonroi.BuildingBlock.RuleEngine");

    private static readonly Counter<long> _failures =
        _meter.CreateCounter<long>("rule_failures", description: "Number of failed rule executions");

    private static readonly Histogram<double> _durations =
        _meter.CreateHistogram<double>("rule_duration_ms", "ms", "Rule execution duration");

    public RuleEngine<T> AddRule(IRule<T> rule, RuleDescriptor descriptor)
    {
        _rules.Add((rule, descriptor));
        InvalidateExecutionPlans();
        return this;
    }

    public RuleEngine<T> AddRule(IRule<T> rule)
    {
        string code = GenerateRuleCode(rule);
        return AddRule(rule, new RuleDescriptor(code, rule.GetType().Name, string.Empty, rule.Type));
    }

    public bool RemoveRule(string ruleCode)
    {
        if (string.IsNullOrWhiteSpace(ruleCode))
        {
            return false;
        }

        int removed = _rules.RemoveAll(x =>
            string.Equals(x.Descriptor.Code, ruleCode, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
        {
            InvalidateExecutionPlans();
        }

        return removed > 0;
    }

    private string GenerateRuleCode(IRule<T> rule)
    {
        string baseCode;
        try
        {
            baseCode = rule.Code;
            if (string.IsNullOrWhiteSpace(baseCode))
            {
                throw new InvalidOperationException("Rule code cannot be empty");
            }
        }
        catch
        {
            int index = _rules.Count(r => r.Rule.GetType() == rule.GetType()) + 1;
            baseCode = $"{rule.GetType().Name}_{index}";
        }

        string code = baseCode;
        int suffix = 1;
        while (_rules.Any(r => r.Descriptor.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
        {
            code = $"{baseCode}_{suffix++}";
        }

        return code;
    }

    public IEnumerable<RuleDescriptor> GetCatalog()
    {
        return _rules.Select(r => r.Descriptor);
    }

    private static List<(IRule<T> Rule, RuleDescriptor Descriptor)> OrderRules(
        IEnumerable<(IRule<T> Rule, RuleDescriptor Descriptor)> rules)
    {
        Dictionary<string, (IRule<T> Rule, RuleDescriptor Descriptor)> dict =
            rules.ToDictionary(r => r.Descriptor.Code, StringComparer.OrdinalIgnoreCase);

        HashSet<string> visiting = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        List<(IRule<T>, RuleDescriptor)> result = [];

        void Visit(string code)
        {
            if (visited.Contains(code))
            {
                return;
            }

            if (!visiting.Add(code))
            {
                throw new InvalidOperationException($"Circular rule dependency detected for '{code}'.");
            }

            if (!dict.TryGetValue(code, out (IRule<T> Rule, RuleDescriptor Descriptor) entry))
            {
                throw new InvalidOperationException($"Rule '{code}' not registered but referenced as a dependency.");
            }

            foreach (string dep in entry.Descriptor.DependsOn)
            {
                if (!dict.ContainsKey(dep))
                {
                    throw new InvalidOperationException($"Missing dependency '{dep}' for rule '{code}'.");
                }

                Visit(dep);
            }

            visiting.Remove(code);
            visited.Add(code);
            result.Add(entry);
        }

        foreach (string? code in dict.Values.OrderBy(r => r.Descriptor.Order).Select(r => r.Descriptor.Code))
        {
            Visit(code);
        }

        return result;
    }

    public Task ExecuteAsync(T context, params RuleType[] ruleTypes)
    {
        return ExecuteAsync(context, null, CancellationToken.None, ruleTypes);
    }

    public Task ExecuteAsync(T context, CancellationToken cancellationToken, params RuleType[] ruleTypes)
    {
        return ExecuteAsync(context, null, cancellationToken, ruleTypes);
    }

    public Task ExecuteAsync(T context, IEnumerable<string>? selectedRuleCodes, params RuleType[] ruleTypes)
    {
        return ExecuteAsync(context, selectedRuleCodes, CancellationToken.None, ruleTypes);
    }

    // Update all calls to ExecuteRulesAsync to pass the cancellationToken parameter
    public async Task ExecuteAsync(
        T context,
        IEnumerable<string>? selectedRuleCodes,
        CancellationToken cancellationToken,
        params RuleType[] ruleTypes)
    {
        if (context is ITransactionalRuleContext { HasActiveTransaction: false } transactional)
        {
            await using IDbContextTransaction? tx = await transactional.BeginTransactionAsync().ConfigureAwait(false);
            try
            {
                await ExecuteRulesAsync(context, selectedRuleCodes, ruleTypes, cancellationToken).ConfigureAwait(false);
                if (tx is not null)
                {
                    await transactional.CommitTransactionAsync(tx).ConfigureAwait(false);
                }
            }
            catch
            {
                transactional.RollbackTransaction();
                throw;
            }

            return;
        }

        await ExecuteRulesAsync(context, selectedRuleCodes, ruleTypes, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteRulesAsync(T context, IEnumerable<string>? selectedRuleCodes, RuleType[] ruleTypes,
        CancellationToken cancellationToken = default)
    {
        licenseGuard?.EnsureFeature(FreeTierFeatures.Premium.RuleEngine);

        // ✅ Security check: cross-tenant access prevention
        if (context is ITenantScoped scoped)
        {
            string? current = TenantContext.CurrentTenantId;
            if (!string.IsNullOrWhiteSpace(current) && scoped.TenantId != current)
            {
                throw new UnauthorizedAccessException("Cross tenant rule execution detected.");
            }
        }

        IEnumerable<(IRule<T> Rule, RuleDescriptor Descriptor)> rulesToRun =
            GetRulesToRun(context, selectedRuleCodes, ruleTypes);

        HashSet<string> executed = new(StringComparer.OrdinalIgnoreCase);
        foreach ((IRule<T> rule, RuleDescriptor descriptor) in GetOrderedPlan(rulesToRun))
        {
            Stopwatch sw = Stopwatch.StartNew();
            string? tenantId = TenantContext.CurrentTenantId;
            try
            {
                await rule.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                sw.Stop();
                logger?.LogInformation("Executed rule {RuleCode} in {Elapsed}ms: PASS", descriptor.Code,
                    sw.ElapsedMilliseconds);
                _durations.Record(
                    sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("rule_code", descriptor.Code),
                    new KeyValuePair<string, object?>("result", "pass"),
                    new KeyValuePair<string, object?>("tenant_id", tenantId));
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger?.LogError(ex, "Executed rule {RuleCode} in {Elapsed}ms: FAIL - {Reason}", descriptor.Code,
                    sw.ElapsedMilliseconds, ex.Message);
                _failures.Add(1,
                    new KeyValuePair<string, object?>("rule_code", descriptor.Code),
                    new KeyValuePair<string, object?>("tenant_id", tenantId));
                _durations.Record(
                    sw.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("rule_code", descriptor.Code),
                    new KeyValuePair<string, object?>("result", "fail"),
                    new KeyValuePair<string, object?>("tenant_id", tenantId));
                throw;
            }

            executed.Add(descriptor.Code);
        }

        string[] unused = [.. _rules.Select(r => r.Descriptor.Code).Except(executed, StringComparer.OrdinalIgnoreCase)];
        if (unused.Length > 0)
        {
            logger?.Warn("Registered rules not executed: {Rules}", string.Join(", ", unused));
        }
    }

    private IEnumerable<(IRule<T> Rule, RuleDescriptor Descriptor)> GetRulesToRun(
        T context,
        IEnumerable<string>? selectedRuleCodes,
        RuleType[] ruleTypes)
    {
        IEnumerable<(IRule<T> Rule, RuleDescriptor Descriptor)> rules = _rules;

        if (ruleTypes is { Length: > 0 })
        {
            rules = rules.Where(r => ruleTypes.Contains(r.Descriptor.HookPoint));
        }

        if (selectedRuleCodes != null && selectedRuleCodes.Any())
        {
            HashSet<string> set = new(selectedRuleCodes, StringComparer.OrdinalIgnoreCase);
            rules = rules.Where(r => set.Contains(r.Descriptor.Code));
        }

        if (options?.CurrentValue?.RuleToggles?.Count > 0)
        {
            Dictionary<string, bool> toggles = options.CurrentValue.RuleToggles;
            rules = rules.Where(r =>
                !toggles.TryGetValue(r.Descriptor.Code, out bool enabled) || enabled);
        }

        // Tenant-specific feature flags override global toggles
        if (options?.CurrentValue?.TenantRuleToggles?.Count > 0)
        {
            string? tenant = TenantContext.CurrentTenantId;
            if (!string.IsNullOrWhiteSpace(tenant) &&
                options.CurrentValue.TenantRuleToggles.TryGetValue(tenant, out Dictionary<string, bool>? tenantToggles))
            {
                rules = rules.Where(r =>
                    !tenantToggles.TryGetValue(r.Descriptor.Code, out bool enabled) || enabled);
            }
        }

        if (activation is not null)
        {
            rules = rules.Where(r => activation.IsActive(r.Rule, context));
        }

        return rules;
    }

    private IReadOnlyList<(IRule<T> Rule, RuleDescriptor Descriptor)> GetOrderedPlan(
        IEnumerable<(IRule<T> Rule, RuleDescriptor Descriptor)> rules)
    {
        (IRule<T> Rule, RuleDescriptor Descriptor)[] snapshot = rules as (IRule<T> Rule, RuleDescriptor Descriptor)[] ?? [.. rules];
        if (snapshot.Length <= 1)
        {
            return snapshot;
        }

        string key = BuildPlanCacheKey(snapshot);
        int version = Volatile.Read(ref _rulesVersion);
        if (_executionPlanCache.TryGetValue(key, out CachedExecutionPlan? cached) && cached.Version == version)
        {
            return cached.Plan;
        }

        List<(IRule<T> Rule, RuleDescriptor Descriptor)> ordered = OrderRules(snapshot);
        _executionPlanCache[key] = new CachedExecutionPlan(version, ordered);
        if (_executionPlanCache.Count > MaxExecutionPlanCacheEntries)
        {
            _executionPlanCache.Clear();
            _executionPlanCache[key] = new CachedExecutionPlan(version, ordered);
        }

        return ordered;
    }

    private static string BuildPlanCacheKey(IReadOnlyList<(IRule<T> Rule, RuleDescriptor Descriptor)> rules)
    {
        return string.Join("|", rules
            .Select(r => r.Descriptor.Code)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase));
    }

    private void InvalidateExecutionPlans()
    {
        Interlocked.Increment(ref _rulesVersion);
        _executionPlanCache.Clear();
    }

    private sealed record CachedExecutionPlan(
        int Version,
        IReadOnlyList<(IRule<T> Rule, RuleDescriptor Descriptor)> Plan);
}
