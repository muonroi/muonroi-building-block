namespace Muonroi.RuleEngine.Core.Tests;

public class RuleOrchestratorEventListenerTests
{
    private sealed class WeatherRule : IRule<string>
    {
        public string Name => "WeatherRule";
        public IEnumerable<Type> Dependencies => [];
        public string Code => "WeatherRule";
        public int Order => 0;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<RuleResult> EvaluateAsync(string ctx, FactBag facts, CancellationToken ct)
        {
            facts["temp"] = 30;
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class RemoveWeatherFactRule : IRule<string>
    {
        public string Name => "RemoveWeatherFactRule";
        public IEnumerable<Type> Dependencies => [];
        public string Code => "RemoveWeatherFactRule";
        public int Order => 1;
        public IReadOnlyList<string> DependsOn => ["WeatherRule"];
        public HookPoint HookPoint => HookPoint.BeforePersist;
        public RuleType Type => RuleType.Validation;

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<RuleResult> EvaluateAsync(string ctx, FactBag facts, CancellationToken ct)
        {
            facts.Remove("temp");
            return Task.FromResult(RuleResult.Passed());
        }
    }

    private sealed class TestListener : IRuleEventListener<string>
    {
        public bool Matched { get; private set; }
        public IReadOnlyDictionary<string, (object? OldValue, object? NewValue)>? Changes { get; private set; }
        public TimeSpan Duration { get; private set; }

        public Task OnRuleMatchedAsync(IRule<string> rule, string context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            Matched = true;
            return Task.CompletedTask;
        }

        public Task OnRuleFiredAsync(IRule<string> rule, RuleResult result, FactBag facts,
            IReadOnlyDictionary<string, (object? OldValue, object? NewValue)> changes, string context,
            TimeSpan duration, CancellationToken cancellationToken = default)
        {
            Changes = changes;
            Duration = duration;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_NotifiesListenersWithFactChanges()
    {
        WeatherRule rule = new();
        TestListener listener = new();
        RuleOrchestrator<string> orchestrator = new([rule], [], null, [listener]);

        FactBag facts = await orchestrator.ExecuteAsync("Hanoi");

        Assert.True(listener.Matched);
        Assert.NotNull(listener.Changes);
        Assert.True(listener.Changes!.ContainsKey("temp"));
        Assert.Equal(30, facts["temp"]);
        Assert.True(listener.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_NotifiesListenersWhenFactRemoved()
    {
        WeatherRule addRule = new();
        RemoveWeatherFactRule removeRule = new();
        TestListener listener = new();
        RuleOrchestrator<string> orchestrator = new([addRule, removeRule], [], null, [listener]);

        await orchestrator.ExecuteAsync("Hanoi");

        Assert.NotNull(listener.Changes);
        Assert.True(listener.Changes!.TryGetValue("temp", out (object? OldValue, object? NewValue) change));
        Assert.Equal(30, change.OldValue);
        Assert.Null(change.NewValue);
    }
}