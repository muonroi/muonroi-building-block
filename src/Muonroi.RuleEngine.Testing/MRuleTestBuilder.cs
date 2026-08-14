namespace Muonroi.RuleEngine.Testing;

/// <summary>
/// Fluent helper for unit and integration-like rule tests.
/// </summary>
public sealed class MRuleTestBuilder<TContext> where TContext : class, new()
{
    private readonly ServiceCollection _services = new();
    private readonly Dictionary<string, object?> _seedFacts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _executedCodes = [];
    private Func<IServiceProvider, Task<MRuleTestResult>>? _execution;
    private TContext _context = new();

    private MRuleTestBuilder()
    {
        _services.AddLogging();
    }

    public static MRuleTestBuilder<TContext> ForRule<TRule>() where TRule : class, IRule<TContext>
    {
        MRuleTestBuilder<TContext> builder = new();
        builder._services.AddScoped<TRule>();
        builder._execution = async provider =>
        {
            TRule rule = provider.GetRequiredService<TRule>();
            FactBag facts = builder.CreateFacts();
            RuleResult result = await rule.EvaluateAsync(builder._context, facts, CancellationToken.None);
            if (result.IsSuccess)
            {
                await rule.ExecuteAsync(builder._context, CancellationToken.None);
            }

            builder._executedCodes.Add(rule.Code);
            return new MRuleTestResult(result.IsSuccess, facts, result, ExecutedRuleCodes: builder._executedCodes);
        };

        return builder;
    }

    public static MRuleTestBuilder<TContext> ForRule(IRule<TContext> rule)
    {
        MRuleTestBuilder<TContext> builder = new();
        builder._services.AddSingleton(rule);
        builder._execution = async _ =>
        {
            FactBag facts = builder.CreateFacts();
            RuleResult result = await rule.EvaluateAsync(builder._context, facts, CancellationToken.None);
            if (result.IsSuccess)
            {
                await rule.ExecuteAsync(builder._context, CancellationToken.None);
            }

            builder._executedCodes.Add(rule.Code);
            return new MRuleTestResult(result.IsSuccess, facts, result, ExecutedRuleCodes: builder._executedCodes);
        };

        return builder;
    }
    public static MRuleTestBuilder<TContext> ForOrchestrator(Action<MRuleEngineBuilder<TContext>> configure)
    {
        MRuleTestBuilder<TContext> builder = new();
        ServiceCollection services = builder._services;
        services.AddScoped<IRuleEventListener<TContext>>(_ => new RecorderListener(builder._executedCodes));
        MRuleEngineBuilder<TContext> ruleBuilder = services.AddRuleEngine<TContext>();
        configure(ruleBuilder);

        builder._execution = async provider =>
        {
            RuleOrchestrator<TContext> orchestrator = provider.GetRequiredService<RuleOrchestrator<TContext>>();
            FactBag facts = await orchestrator.ExecuteAsync(builder._context, cancellationToken: CancellationToken.None);
            return new MRuleTestResult(true, facts, ExecutedRuleCodes: builder._executedCodes);
        };

        return builder;
    }

    public MRuleTestBuilder<TContext> WithContext(Action<TContext> setup)
    {
        setup(_context);
        return this;
    }

    public MRuleTestBuilder<TContext> WithContext(TContext context)
    {
        _context = context;
        return this;
    }

    public MRuleTestBuilder<TContext> WithFact(string key, object? value)
    {
        _seedFacts[key] = value;
        return this;
    }

    public MRuleTestBuilder<TContext> WithService<TService>(TService service) where TService : class
    {
        _services.AddSingleton(service);
        return this;
    }

    public async Task<MRuleTestResult> ExecuteAsync()
    {
        if (_execution is null)
        {
            MGuard.State(false, "No execution strategy configured. Use ForRule or ForOrchestrator.");
        }

        using ServiceProvider provider = _services.BuildServiceProvider();
        try
        {
            return await MGuard.NotNull(_execution)(provider);
        }
        catch (Exception ex)
        {
            return new MRuleTestResult(false, CreateFacts(), Exception: ex, ExecutedRuleCodes: _executedCodes);
        }
    }

    private FactBag CreateFacts()
    {
        FactBag facts = new();
        foreach ((string key, object? value) in _seedFacts)
        {
            facts[key] = value;
        }

        return facts;
    }

    private sealed class RecorderListener(List<string> executedCodes) : IRuleEventListener<TContext>
    {
        public Task OnRuleMatchedAsync(IRule<TContext> rule, TContext context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task OnRuleFiredAsync(IRule<TContext> rule, RuleResult result, FactBag facts,
            IReadOnlyDictionary<string, (object? OldValue, object? NewValue)> changes, TContext context,
            TimeSpan duration, CancellationToken cancellationToken = default)
        {
            executedCodes.Add(rule.Code);
            return Task.CompletedTask;
        }
    }
}
