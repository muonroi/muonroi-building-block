using Muonroi.Logging.Abstractions;
using System.Text.RegularExpressions;

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

    private sealed class ListLogger<T> : IMLog<T>
    {
        public List<string> Logs { get; } = [];


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
            Logs.Add(FormatMessage(messageTemplate, args));
        }

        public void Warn(string messageTemplate, params object?[] args)
        {
            Logs.Add(FormatMessage(messageTemplate, args));
        }

        public void Error(Exception? ex, string messageTemplate, params object?[] args)
        {
            Logs.Add(FormatMessage(messageTemplate, args));
        }

        public void Debug(string messageTemplate, params object?[] args)
        {
            Logs.Add(FormatMessage(messageTemplate, args));
        }

        public void InfoTrace(string messageTemplate, params object?[] args)
        {
            Logs.Add(FormatMessage(messageTemplate, args));
        }

        public void InfoContext(string messageTemplate, params object?[] args) => Info(messageTemplate, args);
        public void InfoContext(string messageTemplate, object? arg0 = null, object? arg1 = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) => Info(messageTemplate, arg0, arg1);
        public void ErrorContext(Exception? ex, string messageTemplate, params object?[] args) => Error(ex, messageTemplate, args);
        public void ErrorContext(Exception? ex, string messageTemplate, object? arg0 = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) => Error(ex, messageTemplate, arg0);
        public void Audit(string messageTemplate, params object?[] args) => Info(messageTemplate, args);
        public void Audit(string messageTemplate, string? auditType = null, string? action = null, bool isSuccess = true, string? targetId = null, string? targetType = null, object? metadata = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) => Info(messageTemplate, auditType, action);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return NullScope.Instance;
        }

        private static string FormatMessage(string template, params object?[] args)
        {
            int index = 0;
            return Regex.Replace(template, @"\{[^}]+\}", _ =>
            {
                if (index >= args.Length)
                {
                    return string.Empty;
                }

                object? value = args[index++];
                return value?.ToString() ?? string.Empty;
            });
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

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
