using FluentAssertions;
using Moq;
using Microsoft.Extensions.Logging;
using Muonroi.Logging.Abstractions;
using Muonroi.Resilience.Policies;
using Polly;
using Polly.CircuitBreaker;
using Xunit;

namespace Muonroi.Resilience.Tests;
public class ResilienceTests
{
    [Fact]
    public async Task CreateDefaultPipeline_Retry_ShouldExecuteMultipleTimes()
    {
        // Arrange
        Mock<IMLog<PolicyHandler>> loggerMock = new();
        PolicyHandler handler = new(loggerMock.Object);
        ResiliencePipeline<string> pipeline = handler.CreateDefaultPipeline<string>("TestService");

        int executions = 0;

        // Act
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await pipeline.ExecuteAsync(ct =>
            {
                executions++;

                return ValueTask.FromException<string>(
                    new Exception("Failure"));
            });
        });

        // Assert
        Assert.Equal(4, executions);
    }

    [Fact]
    public async Task CreateDefaultPipeline_Success_ShouldReturnValueWithoutRetry()
    {
        TestLogger logger = new();
        PolicyHandler handler = new(logger);
        ResiliencePipeline<string> pipeline = handler.CreateDefaultPipeline<string>("TestService");

        int executions = 0;

        string result = await pipeline.ExecuteAsync(_ =>
        {
            executions++;
            return ValueTask.FromResult("ok");
        });

        result.Should().Be("ok");
        executions.Should().Be(1);
        logger.WarningMessages.Should().BeEmpty();
        logger.ErrorMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDefaultPipeline_Retry_ShouldLogWarningsForEachRetry()
    {
        TestLogger logger = new();
        PolicyHandler handler = new(logger);
        ResiliencePipeline<string> pipeline = handler.CreateDefaultPipeline<string>("Payments");

        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await pipeline.ExecuteAsync(_ => ValueTask.FromException<string>(new Exception("Failure")));
        });

        logger.WarningMessages.Should().HaveCount(3);
        logger.WarningMessages.Should().OnlyContain(x => x.Contains("Retrying Payments", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateDefaultPipeline_CircuitBreaker_ShouldOpenAfterRepeatedFailures()
    {
        TestLogger logger = new();
        PolicyHandler handler = new(logger);
        ResiliencePipeline<string> pipeline = handler.CreateDefaultPipeline<string>("Billing");

        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await pipeline.ExecuteAsync(_ => ValueTask.FromException<string>(new Exception("Failure")));
        });

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await pipeline.ExecuteAsync(_ => ValueTask.FromException<string>(new Exception("Failure")));
        });

        Func<Task> action = async () => await pipeline.ExecuteAsync(_ => ValueTask.FromResult("unreachable"));

        await action.Should().ThrowAsync<BrokenCircuitException>();
        logger.ErrorMessages.Should().Contain(x => x.Contains("Circuit breaker opened for Billing", StringComparison.Ordinal));
    }

    private sealed class TestLogger : IMLog<PolicyHandler>
    {
        public List<string> WarningMessages { get; } = [];
        public List<string> ErrorMessages { get; } = [];
        public List<string> InfoMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            switch (logLevel)
            {
                case LogLevel.Warning:
                    WarningMessages.Add(message);
                    break;
                case LogLevel.Error:
                    ErrorMessages.Add(message);
                    break;
                case LogLevel.Information:
                    InfoMessages.Add(message);
                    break;
            }
        }

        public IMLogContextScope BeginProperty(string key, object? value) => new NullLogContextScope();

        public void Info(string messageTemplate, params object?[] args)
            => InfoMessages.Add(string.Format(messageTemplate.Replace("{ServiceName}", "{0}"), args));

        public void Warn(string messageTemplate, params object?[] args)
            => WarningMessages.Add(string.Format(messageTemplate, args));

        public void Error(Exception? ex, string messageTemplate, params object?[] args)
            => ErrorMessages.Add(string.Format(messageTemplate, args));

        public void Debug(string messageTemplate, params object?[] args) { }

        public void InfoTrace(string messageTemplate, params object?[] args) { }
        
        public void InfoContext(
            string messageTemplate,
            object? request = null,
            object? response = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMethod = "",
            [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0) { }

        public void ErrorContext(
            Exception exception,
            string messageTemplate,
            object? contextData = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMethod = "",
            [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0) { }

        public void Audit(
            string action,
            string entityType,
            string entityId,
            bool isSuccess,
            string? actorId = null,
            string? tenantId = null,
            object? changes = null,
            [System.Runtime.CompilerServices.CallerMemberName] string callerMethod = "",
            [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "",
            [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0) { }
    }

    private sealed class NullLogContextScope : IMLogContextScope
    {
        public void Dispose() { }
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose() { }
    }
}
