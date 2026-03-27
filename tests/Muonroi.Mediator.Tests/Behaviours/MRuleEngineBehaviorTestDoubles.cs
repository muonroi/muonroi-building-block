using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Behaviours;
using Muonroi.Mediator.Mediator.Interfaces;
using Muonroi.RuleEngine.Abstractions;
using System.Collections.Concurrent;

namespace Muonroi.Mediator.Tests.Behaviours;

/// <summary>
/// In-memory logger used to capture structured mediator rule log entries in tests.
/// </summary>
/// <typeparam name="T">The logger category type.</typeparam>
internal sealed class LoggerSpy<T> : IMLog<T>
{
    private readonly Stack<KeyValuePair<string, object?>> _properties = new();

    /// <summary>
    /// Gets the log entries captured during the test.
    /// </summary>
    public List<LogEntry> Entries { get; } = [];

    /// <inheritdoc/>
    public IMLogContextScope BeginProperty(string key, object? value)
    {
        _properties.Push(new KeyValuePair<string, object?>(key, value));
        return new Scope(this);
    }

    /// <inheritdoc/>
    public void Info(string messageTemplate, params object?[] args)
    {
        Entries.Add(CreateEntry(LogLevel.Information, messageTemplate, null, args));
    }

    /// <inheritdoc/>
    public void Warn(string messageTemplate, params object?[] args)
    {
        Entries.Add(CreateEntry(LogLevel.Warning, messageTemplate, null, args));
    }

    /// <inheritdoc/>
    public void Error(Exception? ex, string messageTemplate, params object?[] args)
    {
        Entries.Add(CreateEntry(LogLevel.Error, messageTemplate, ex, args));
    }

    /// <inheritdoc/>
    public void Debug(string messageTemplate, params object?[] args)
    {
        Entries.Add(CreateEntry(LogLevel.Debug, messageTemplate, null, args));
    }

    /// <inheritdoc/>
    public void InfoTrace(string messageTemplate, params object?[] args)
    {
        Entries.Add(CreateEntry(LogLevel.Information, messageTemplate, null, args));
    }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(CreateEntry(logLevel, formatter(state, exception), exception, []));
    }

    /// <summary>
    /// Creates a captured log entry from the current scope state.
    /// </summary>
    /// <param name="level">The log level.</param>
    /// <param name="template">The message template.</param>
    /// <param name="exception">The logged exception.</param>
    /// <param name="args">The message arguments.</param>
    /// <returns>The captured log entry.</returns>
    private LogEntry CreateEntry(LogLevel level, string template, Exception? exception, object?[] args)
    {
        object? scope = _properties
            .Select(static x => x.Value)
            .FirstOrDefault(static value => value?.GetType().Name == "RuleExecutionLogScope");

        return new LogEntry(level, template, exception, args, scope);
    }

    /// <summary>
    /// Disposable scope used to unwind structured log properties.
    /// </summary>
    private sealed class Scope(LoggerSpy<T> owner) : IMLogContextScope
    {
        /// <inheritdoc/>
        public void Dispose()
        {
            if (owner._properties.Count > 0)
            {
                _ = owner._properties.Pop();
            }
        }
    }
}

/// <summary>
/// Captured log entry used by mediator behavior tests.
/// </summary>
/// <param name="Level">The log level.</param>
/// <param name="MessageTemplate">The original message template.</param>
/// <param name="Exception">The logged exception.</param>
/// <param name="Args">The structured arguments.</param>
/// <param name="Scope">The captured structured scope payload.</param>
internal sealed record LogEntry(
    LogLevel Level,
    string MessageTemplate,
    Exception? Exception,
    IReadOnlyList<object?> Args,
    object? Scope);

/// <summary>
/// In-memory mediator used to capture published notifications.
/// </summary>
internal sealed class MediatorSpy : IMediator
{
    private readonly ConcurrentQueue<object> _published = new();

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Publish(object,CancellationToken)"/> should throw.
    /// </summary>
    public bool ThrowOnPublish { get; set; }

    /// <summary>
    /// Gets the notifications published during the test.
    /// </summary>
    public IReadOnlyCollection<object> Published => _published.ToArray();

    /// <inheritdoc/>
    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        return Publish((object)notification, cancellationToken);
    }

    /// <inheritdoc/>
    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        if (ThrowOnPublish)
        {
            throw new InvalidOperationException("publish failed");
        }

        _published.Enqueue(notification);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Test fixture that wires the mediator rule behavior to in-memory rules, logging, and notification publishing.
/// </summary>
/// <typeparam name="TRuleContext">The rule context type resolved by the behavior.</typeparam>
internal sealed class RuleBehaviorFixture<TRuleContext>
    where TRuleContext : class
{
    private readonly List<object> _rules = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleBehaviorFixture{TRuleContext}"/> class.
    /// </summary>
    /// <param name="additionalFactory">Optional fallback service factory for additional dependencies.</param>
    public RuleBehaviorFixture(ServiceFactory? additionalFactory = null)
    {
        Logger = new LoggerSpy<MRuleEngineBehavior<TestRuleRequest, string>>();
        Mediator = new MediatorSpy();
        ExecutionContextAccessor = new SystemExecutionContextAccessor();
        ExecutionContextAccessor.Set(new SystemExecutionContext(
            tenantId: "tenant-a",
            userId: "user-a",
            username: "alice",
            correlationId: "corr-a",
            accessToken: null,
            apiKey: null,
            isAuthenticated: true,
            permissions: [],
            sourceType: "tests"));
        ServiceFactory = serviceType =>
        {
            if (serviceType == typeof(IEnumerable<IRule<TRuleContext>>))
            {
                return _rules.Cast<IRule<TRuleContext>>().ToList();
            }

            if (serviceType == typeof(IMediator))
            {
                return Mediator;
            }

            return additionalFactory?.Invoke(serviceType);
        };
    }

    /// <summary>
    /// Gets the logger spy used by the behavior under test.
    /// </summary>
    public LoggerSpy<MRuleEngineBehavior<TestRuleRequest, string>> Logger { get; }

    /// <summary>
    /// Gets the mediator spy used by the behavior under test.
    /// </summary>
    public MediatorSpy Mediator { get; }

    /// <summary>
    /// Gets the execution context accessor used to provide tenant information.
    /// </summary>
    public ISystemExecutionContextAccessor ExecutionContextAccessor { get; }

    /// <summary>
    /// Gets the service factory supplied to <see cref="MRuleEngineBehavior{TRequest,TResponse}"/>.
    /// </summary>
    public ServiceFactory ServiceFactory { get; }

    /// <summary>
    /// Adds a rule instance to the in-memory fixture.
    /// </summary>
    /// <param name="rule">The rule instance to register.</param>
    public void AddRule(object rule)
    {
        _rules.Add(rule);
    }
}

/// <summary>
/// Test rule context used by mediator rule behavior tests.
/// </summary>
/// <param name="orderId">The order identifier associated with the request.</param>
internal sealed class TestRuleContext(string orderId) : IRuleContext
{
    /// <summary>
    /// Gets the order identifier associated with the current request.
    /// </summary>
    public string OrderId { get; } = orderId;

    /// <inheritdoc/>
    public void HaltGroup()
    {
    }
}

/// <summary>
/// Test mediator request that participates in the rule pipeline.
/// </summary>
/// <param name="OrderId">The order identifier.</param>
internal sealed record TestRuleRequest(string OrderId) : IMRuleRequest<string, TestRuleContext>
{
    /// <inheritdoc/>
    public TestRuleContext BuildRuleContext() => new(OrderId);
}

/// <summary>
/// Notification used by mediator behavior tests.
/// </summary>
internal sealed class TestNotification : INotification
{
    /// <summary>
    /// Gets or sets the notification payload.
    /// </summary>
    public string? Payload { get; set; }
}

/// <summary>
/// Secondary notification used to verify multiple emit-on-pass declarations.
/// </summary>
internal sealed class SecondaryNotification : INotification
{
    /// <summary>
    /// Gets or sets the notification payload.
    /// </summary>
    public string? Payload { get; set; }
}
