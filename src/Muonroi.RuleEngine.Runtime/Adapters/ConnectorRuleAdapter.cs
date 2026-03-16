using System.Diagnostics;
using System.Text.Json;
using Muonroi.Integration.Abstractions;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Abstractions.Adapters;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Adapts an <see cref="IServiceTaskConnector"/> into an <see cref="IRule{TContext}"/>
/// so that connectors participate in the flow graph like any other rule.
/// </summary>
/// <typeparam name="TContext">The rule execution context type.</typeparam>
public sealed class ConnectorRuleAdapter<TContext> : IRule<TContext>
{
    private static readonly ActivitySource ActivitySource = new("Muonroi.Integration");

    private readonly string _code;
    private readonly string _connectorType;
    private readonly JsonElement? _connectorConfig;
    private readonly string? _credentialId;
    private readonly IConnectorRegistry _registry;
    private readonly IConnectorCredentialStore? _credentialStore;
    private readonly IContextProjector<TContext> _projector;
    private readonly IMLog<ConnectorRuleAdapter<TContext>> _log;
    private readonly string? _tenantId;

    public string Code => _code;
    public int Order { get; init; }
    public string[] DependsOn { get; init; } = [];
    public HookPoint HookPoint => HookPoint.BeforeRule;
    public RuleType Type => RuleType.Business;
    public string Name => $"Connector:{_connectorType}:{_code}";
    public IEnumerable<Type> Dependencies => [];

    public ConnectorRuleAdapter(
        string code,
        string connectorType,
        JsonElement? connectorConfig,
        string? credentialId,
        IConnectorRegistry registry,
        IConnectorCredentialStore? credentialStore,
        IContextProjector<TContext> projector,
        IMLog<ConnectorRuleAdapter<TContext>> log,
        string? tenantId = null)
    {
        _code = code;
        _connectorType = connectorType;
        _connectorConfig = connectorConfig;
        _credentialId = credentialId;
        _registry = registry;
        _credentialStore = credentialStore;
        _projector = projector;
        _log = log;
        _tenantId = tenantId;
    }

    public async Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        IServiceTaskConnector? connector = _registry.Resolve(_connectorType);
        if (connector is null)
        {
            _log.Warn("Connector type '{ConnectorType}' not found in registry.", _connectorType);
            return RuleResult.Failure($"Connector type '{_connectorType}' not found.");
        }

        using Activity? activity = ActivitySource.StartActivity($"connector.execute.{_connectorType}", ActivityKind.Client);
        activity?.SetTag("connector.type", _connectorType);
        activity?.SetTag("connector.node", _code);
        activity?.SetTag("tenant.id", _tenantId);

        // Load credentials
        IReadOnlyDictionary<string, string> credentials = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(_credentialId) && _credentialStore is not null)
        {
            try
            {
                credentials = await _credentialStore.GetAsync(_credentialId, _tenantId, ct);
            }
            catch (Exception ex)
            {
                _log.Warn("Failed to load credentials '{CredentialId}': {Message}", _credentialId, ex.Message);
                return RuleResult.Failure($"Failed to load credentials for connector '{_code}': {ex.Message}");
            }
        }

        // Build connector config JSON document
        JsonDocument configDoc = _connectorConfig.HasValue
            ? JsonDocument.Parse(_connectorConfig.Value.GetRawText())
            : JsonDocument.Parse("{}");

        ConnectorContext connectorContext = new()
        {
            Config = configDoc,
            InputFacts = facts,
            Credentials = credentials,
            TenantId = _tenantId,
            CorrelationId = Activity.Current?.TraceId.ToString()
        };

        ConnectorResult result;
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            result = await connector.ExecuteAsync(connectorContext, ct);
            sw.Stop();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _log.Error(ex, "Connector '{ConnectorType}' threw: {Message}", _connectorType, ex.Message);
            activity?.SetTag("connector.status", "error");
            return RuleResult.Failure($"Connector '{_connectorType}' error in '{_code}': {ex.Message}");
        }

        activity?.SetTag("connector.status", result.Success ? "success" : "failure");
        activity?.SetTag("connector.duration_ms", sw.ElapsedMilliseconds);

        // Write output facts to FactBag
        foreach (KeyValuePair<string, object?> kv in result.OutputFacts)
        {
            facts.Set(kv.Key, kv.Value);
            facts.Set($"__node.{_code}.{kv.Key}", kv.Value);
        }

        // Write connector metadata to FactBag
        facts.Set($"__connector.{_code}.success", result.Success);
        facts.Set($"__connector.{_code}.statusCode", result.StatusCode);
        facts.Set($"__connector.{_code}.duration", result.Duration.TotalMilliseconds);
        if (result.ErrorMessage is not null)
        {
            facts.Set($"__connector.{_code}.error", result.ErrorMessage);
        }

        if (!result.Success)
        {
            _log.Warn("Connector '{ConnectorType}' failed: {Error}", _connectorType, result.ErrorMessage);
            return RuleResult.Failure($"Connector '{_connectorType}' failed: {result.ErrorMessage}");
        }

        return RuleResult.Passed();
    }

    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
