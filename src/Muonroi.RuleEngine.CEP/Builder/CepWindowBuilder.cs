using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.RuleEngine.CEP.Builder;

/// <summary>
/// Entry point for building CEP configurations and runtime windows.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "CEP configuration builder uses null-forgiving on values validated non-null by the fluent builder state machine.")]
public static class CepWindowBuilder
{
    /// <summary>
    /// Starts a fluent configuration definition.
    /// </summary>
    public static CepConfigBuilder Named(string name)
    {
        return new CepConfigBuilder(name);
    }

    /// <summary>
    /// Starts a runtime window definition from an existing persisted config.
    /// </summary>
    public static CepWindowRuntimeBuilder<TPayload> For<TPayload>(CepConfig config)
    {
        return new CepWindowRuntimeBuilder<TPayload>(config);
    }
}

/// <summary>
/// Fluent builder for <see cref="CepConfig"/>.
/// </summary>
/// <param name="name">Configuration name.</param>
public sealed class CepConfigBuilder(string name)
{
    private readonly string _name = NormalizeRequired(name, nameof(name));
    private readonly Dictionary<string, string> _metadata = new(StringComparer.OrdinalIgnoreCase);
    private string? _tenantId;
    private string? _description;
    private WindowType _windowType = WindowType.Sliding;
    private TimeSpan _windowSize = TimeSpan.FromMinutes(1);
    private TimeSpan _timeToLive = TimeSpan.FromMinutes(5);
    private string _correlationKey = "default";

    /// <summary>
    /// Sets the tenant that owns the config.
    /// </summary>
    public CepConfigBuilder ForTenant(string? tenantId)
    {
        _tenantId = NormalizeOptional(tenantId);
        return this;
    }

    /// <summary>
    /// Sets a human-readable description.
    /// </summary>
    public CepConfigBuilder Describe(string? description)
    {
        _description = NormalizeOptional(description);
        return this;
    }

    /// <summary>
    /// Configures a sliding window.
    /// </summary>
    public CepConfigBuilder Sliding(TimeSpan windowSize)
    {
        _windowType = WindowType.Sliding;
        _windowSize = ValidateDuration(windowSize, nameof(windowSize));
        if (_timeToLive < _windowSize)
        {
            _timeToLive = _windowSize;
        }

        return this;
    }

    /// <summary>
    /// Configures a tumbling window.
    /// </summary>
    public CepConfigBuilder Tumbling(TimeSpan windowSize)
    {
        _windowType = WindowType.Tumbling;
        _windowSize = ValidateDuration(windowSize, nameof(windowSize));
        if (_timeToLive < _windowSize)
        {
            _timeToLive = _windowSize;
        }

        return this;
    }

    /// <summary>
    /// Configures how long events are retained in memory.
    /// </summary>
    public CepConfigBuilder KeepEventsFor(TimeSpan timeToLive)
    {
        _timeToLive = ValidateDuration(timeToLive, nameof(timeToLive));
        MGuard.Against(_timeToLive < _windowSize, "Time to live must be greater than or equal to the window size.");

        return this;
    }

    /// <summary>
    /// Sets the logical field used for correlation.
    /// </summary>
    public CepConfigBuilder CorrelateBy(string correlationKey)
    {
        _correlationKey = NormalizeRequired(correlationKey, nameof(correlationKey));
        return this;
    }

    /// <summary>
    /// Adds metadata used by the host application.
    /// </summary>
    public CepConfigBuilder WithMetadata(string key, string value)
    {
        _metadata[NormalizeRequired(key, nameof(key))] = NormalizeRequired(value, nameof(value));
        return this;
    }

    /// <summary>
    /// Builds a normalized configuration instance.
    /// </summary>
    public CepConfig Build(string? id = null, DateTime? observedAtUtc = null)
    {
        DateTime timestamp = observedAtUtc.HasValue
            ? EnsureUtc(observedAtUtc.Value)
            : DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

        string configId = NormalizeOptional(id) ?? CreateId(_name);
        return new CepConfig
        {
            Id = configId,
            TenantId = _tenantId ?? "_global",
            Name = _name,
            Description = _description,
            WindowType = _windowType,
            WindowSize = _windowSize,
            TimeToLive = _timeToLive,
            CorrelationKey = _correlationKey,
            Metadata = new Dictionary<string, string>(_metadata, StringComparer.OrdinalIgnoreCase),
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp
        };
    }

    private static TimeSpan ValidateDuration(TimeSpan value, string paramName)
    {
        MGuard.Against(value <= TimeSpan.Zero, "Duration must be greater than zero.");

        return value;
    }

    private static string CreateId(string value)
    {
        char[] chars = [.. value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')];

        string collapsed = string.Join(
            '-',
            new string(chars)
                .Split('-', StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(collapsed) ? "cep-window" : collapsed;
    }

    private static string NormalizeRequired(string? value, string paramName)
    {
        return MGuard.NotEmpty(value, paramName).Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
    }
}

/// <summary>
/// Fluent builder for a runtime CEP window.
/// </summary>
/// <param name="config">Configuration to bind.</param>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "Key selector is guaranteed non-null by MGuard.State precondition check immediately before access.")]
public sealed class CepWindowRuntimeBuilder<TPayload>(CepConfig config)
{
    private readonly CepConfig _config = MGuard.NotNull(config);
    private Func<TPayload, string>? _keySelector;

    /// <summary>
    /// Configures a key selector used to correlate payloads.
    /// </summary>
    public CepWindowRuntimeBuilder<TPayload> CorrelateBy(Func<TPayload, string> keySelector)
    {
        _keySelector = MGuard.NotNull(keySelector);
        return this;
    }

    /// <summary>
    /// Builds a runtime window wrapper that records telemetry while reusing the config.
    /// </summary>
    public CepWindow<TPayload> Build()
    {
        MGuard.State(_keySelector is not null, "A correlation key selector must be provided.");

        return new CepWindow<TPayload>(_config, _keySelector!);
    }
}

/// <summary>
/// Runtime wrapper that binds a <see cref="CepConfig"/> to a concrete payload type.
/// </summary>
public sealed class CepWindow<TPayload>
{
    private readonly CepEngine<TPayload> _engine;
    private readonly Func<TPayload, string> _keySelector;

    internal CepWindow(CepConfig config, Func<TPayload, string> keySelector)
    {
        Config = MGuard.NotNull(config);
        _keySelector = MGuard.NotNull(keySelector);
        _engine = new CepEngine<TPayload>(config.WindowSize, config.WindowType, config.TimeToLive);
    }

    /// <summary>
    /// Gets the configuration applied to the window.
    /// </summary>
    public CepConfig Config { get; }

    /// <summary>
    /// Adds an event using the configured key selector.
    /// </summary>
    public IReadOnlyList<CepEvent<TPayload>> Add(TPayload payload, DateTime timestamp)
    {
        string key = NormalizeKey(_keySelector(payload));
        return Add(key, payload, timestamp);
    }

    /// <summary>
    /// Adds an event using an explicit key.
    /// </summary>
    public IReadOnlyList<CepEvent<TPayload>> Add(string key, TPayload payload, DateTime timestamp)
    {
        return _engine.AddEvent(
            NormalizeKey(key),
            payload,
            timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime(),
            Config.Id,
            Config.TenantId);
    }

    private static string NormalizeKey(string? key)
    {
        return string.IsNullOrWhiteSpace(key) ? "default" : key.Trim();
    }
}
