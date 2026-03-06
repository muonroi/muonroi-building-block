namespace Muonroi.RuleEngine.Core.Tests;

public class RuleOrchestratorLoggingTests
{
    private interface IWeatherService
    {
        Task<int> GetTemperatureAsync(string city);
    }

    private sealed class FakeWeatherService(int temperature) : IWeatherService
    {
        public Task<int> GetTemperatureAsync(string city)
        {
            return Task.FromResult(temperature);
        }
    }

    private sealed class WeatherRule(IWeatherService service) : IRule<string>
    {
        public string Name => "WeatherRule";
        public IEnumerable<Type> Dependencies => [];

        public string Code => "WeatherRule";

        public int Order => 0;

        public IReadOnlyList<string> DependsOn => [];

        public HookPoint HookPoint => HookPoint.BeforePersist;

        public RuleType Type => RuleType.Validation;

        public async Task<RuleResult> EvaluateAsync(string context, FactBag facts,
            CancellationToken cancellationToken = default)
        {
            int temp = await service.GetTemperatureAsync(context);
            facts["temp"] = temp;
            return RuleResult.Passed();
        }

        public Task ExecuteAsync(string context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<string> Logs { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class TestHook : IHookHandler<string>
    {
        public List<(HookPoint Point, string Context, TimeSpan? Duration)> Calls { get; } = [];

        public Task HandleAsync(HookPoint point, IRule<string> rule, RuleResult result, FactBag facts, string context,
            TimeSpan? duration = null, CancellationToken cancellationToken = default)
        {
            Calls.Add((point, context, duration));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_LogsAndCanMockExternalService()
    {
        ListLogger<RuleOrchestrator<string>> logger = new();
        TestHook hook = new();
        WeatherRule rule = new(new FakeWeatherService(30));
        RuleOrchestrator<string> orchestrator = new([rule], [hook], logger);

        FactBag facts = await orchestrator.ExecuteAsync("Hanoi");

        Assert.Equal(30, facts["temp"]);
        Assert.Contains(logger.Logs, m => m.Contains("Executing rule WeatherRule"));
        Assert.Contains(logger.Logs, m => m.Contains("Rule WeatherRule succeeded"));
        Assert.Contains(hook.Calls, c => c is { Point: HookPoint.AfterRule, Duration: not null });
        Assert.Contains(hook.Calls, c => c.Context == "Hanoi");
    }
}
