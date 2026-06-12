using System.Text.RegularExpressions;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Behaviours;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.Mediator.Tests.Behaviours;

public class LoggingBehaviorTests
{
    private record TestRequest(string Name) : IRequest<string>;

    private sealed class ListLogger<T> : IMLog<T>
    {
        public List<string> Messages { get; } = [];

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        public IMLogContextScope BeginProperty(string key, object? value) => NullLogScope.Instance;

        public void Info(string messageTemplate, params object?[] args) =>
            Messages.Add(Format(messageTemplate, args));

        public void Warn(string messageTemplate, params object?[] args) =>
            Messages.Add(Format(messageTemplate, args));

        public void Error(Exception? ex, string messageTemplate, params object?[] args) =>
            Messages.Add(Format(messageTemplate, args));

        public void Debug(string messageTemplate, params object?[] args) =>
            Messages.Add(Format(messageTemplate, args));

        public void InfoTrace(string messageTemplate, params object?[] args) =>
            Messages.Add(Format(messageTemplate, args));

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        private static string Format(string template, params object?[] args)
        {
            int index = 0;
            return Regex.Replace(template, @"\{[^}]+\}", _ =>
                index < args.Length ? args[index++]?.ToString() ?? "" : "");
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }

        private sealed class NullLogScope : IMLogContextScope
        {
            public static readonly NullLogScope Instance = new();
            public void Dispose() { }
        }
    }

    [Fact]
    public async Task Handle_Success_LogsHandlingAndHandled()
    {
        var logger = new ListLogger<LoggingBehavior<TestRequest, string>>();
        var behavior = new LoggingBehavior<TestRequest, string>(logger);

        string result = await behavior.Handle(
            new TestRequest("test"),
            () => Task.FromResult("OK"),
            CancellationToken.None);

        result.Should().Be("OK");
        logger.Messages.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Handle_Exception_LogsWarningAndRethrows()
    {
        var logger = new ListLogger<LoggingBehavior<TestRequest, string>>();
        var behavior = new LoggingBehavior<TestRequest, string>(logger);

        Func<Task> act = () => behavior.Handle(
            new TestRequest("test"),
            () => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        logger.Messages.Should().HaveCountGreaterThanOrEqualTo(2);
    }
}
