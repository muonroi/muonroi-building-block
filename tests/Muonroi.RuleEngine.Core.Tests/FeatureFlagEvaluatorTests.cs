using Muonroi.Logging.Abstractions;

namespace Muonroi.RuleEngine.Core.Tests;

public class FeatureFlagEvaluatorTests
{
    private sealed class FakeClient(bool enabled) : IFeatureFlagClient
    {
        public bool IsEnabled(string flag, FeatureContext context)
        {
            return enabled;
        }
    }

    private sealed class ListLogger<T> : IMLog<T>
    {
        public List<string> Logs { get; } = [];

        IDisposable? ILogger.BeginScope<TState>(TState state)
        {
            return NullDisposable.Instance;
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

        public IMLogContextScope BeginProperty(string key, object? value)
        {
            return NullLogScope.Instance;
        }

        public void Info(string messageTemplate, params object?[] args)
        {
            Logs.Add(messageTemplate);
        }

        public void Warn(string messageTemplate, params object?[] args)
        {
            Logs.Add(messageTemplate);
        }

        public void Error(Exception? ex, string messageTemplate, params object?[] args)
        {
            Logs.Add(messageTemplate);
        }

        public void Debug(string messageTemplate, params object?[] args)
        {
            Logs.Add(messageTemplate);
        }

        public void InfoTrace(string messageTemplate, params object?[] args)
        {
            Logs.Add(messageTemplate);
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }

        private sealed class NullLogScope : IMLogContextScope
        {
            public static readonly NullLogScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    [Fact]
    public void FlagOff_ShadowLogsDifference()
    {
        ListLogger<FeatureFlagEvaluator> logger = new();
        FeatureFlagEvaluator evaluator = new(new FakeClient(false), logger);

        int result = evaluator.Evaluate("ruleset", new FeatureContext("t1"), () => 1, () => 2);

        Assert.Equal(1, result);
        Assert.Single(logger.Logs);
    }

    [Fact]
    public void FlagOn_UsesNewRuleSet()
    {
        ListLogger<FeatureFlagEvaluator> logger = new();
        FeatureFlagEvaluator evaluator = new(new FakeClient(true), logger);

        int result = evaluator.Evaluate("ruleset", new FeatureContext("t1"), () => 1, () => 2);

        Assert.Equal(2, result);
        Assert.Empty(logger.Logs);
    }
}
