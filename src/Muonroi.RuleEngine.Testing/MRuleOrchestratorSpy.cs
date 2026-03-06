using Muonroi.Core.Abstractions.SeedWorks;

namespace Muonroi.RuleEngine.Testing;

/// <summary>
/// Spy wrapper that captures orchestrator execution order and fact changes.
/// </summary>
public sealed class MRuleOrchestratorSpy<TContext>
{
    private readonly SpyListener _listener = new();
    private readonly RuleOrchestrator<TContext> _orchestrator;

    public MRuleOrchestratorSpy(
        IEnumerable<IRule<TContext>> rules,
        IEnumerable<IHookHandler<TContext>>? hooks = null,
        ILogger<RuleOrchestrator<TContext>>? logger = null)
    {
        _orchestrator = new RuleOrchestrator<TContext>(
            rules,
            hooks ?? [],
            logger,
            new MJsonSerializeService(),
            [_listener]);
    }

    public IReadOnlyList<MRuleExecutionRecord> ExecutionRecords => _listener.Records;
    public IReadOnlyDictionary<string, object?> BeforeSnapshot { get; private set; } = new Dictionary<string, object?>();
    public IReadOnlyDictionary<string, object?> AfterSnapshot { get; private set; } = new Dictionary<string, object?>();

    public async Task<FactBag> ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        BeforeSnapshot = new Dictionary<string, object?>();
        FactBag facts = await _orchestrator.ExecuteAsync(context, cancellationToken: cancellationToken);
        AfterSnapshot = facts.AsReadOnly();
        return facts;
    }

    private sealed class SpyListener : IRuleEventListener<TContext>
    {
        private readonly List<MRuleExecutionRecord> _records = [];
        public IReadOnlyList<MRuleExecutionRecord> Records => _records;

        public Task OnRuleMatchedAsync(IRule<TContext> rule, TContext context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task OnRuleFiredAsync(IRule<TContext> rule, RuleResult result, FactBag facts,
            IReadOnlyDictionary<string, (object? OldValue, object? NewValue)> changes, TContext context,
            TimeSpan duration, CancellationToken cancellationToken = default)
        {
            _records.Add(new MRuleExecutionRecord(rule.Code, result.IsSuccess, duration, changes));
            return Task.CompletedTask;
        }
    }
}
