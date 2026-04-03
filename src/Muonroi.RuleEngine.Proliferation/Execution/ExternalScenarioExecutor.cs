using System.Diagnostics;
using System.Text.Json;
using Muonroi.Integration.Abstractions;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Proliferation.Auth;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Execution;

/// <summary>
/// Executes proliferation scenarios against external project HTTP endpoints
/// via the existing IServiceTaskConnector infrastructure (HttpConnector).
/// Reuses the connector system per architecture decision — no IHttpClientFactory directly.
///
/// Auth integration: IAuthStrategyResolver is called before each execution to resolve
/// dynamic credentials (OAuth2 tokens, mTLS, static headers with optional rotation).
/// 401 retry: if OAuth2ClientCredentials strategy returns 401, the token cache is
/// invalidated and the request retried once with a fresh token.
/// </summary>
public sealed class ExternalScenarioExecutor : IExternalScenarioExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IServiceTaskConnector _connector;
    private readonly IMLog<ExternalScenarioExecutor>? _logger;
    private readonly IAuthStrategyResolver? _authResolver;
    private readonly IOAuth2TokenProvider? _oauth2TokenProvider;

    /// <summary>
    /// Constructor with auth resolver (full feature set).
    /// </summary>
    public ExternalScenarioExecutor(
        IServiceTaskConnector connector,
        IAuthStrategyResolver? authResolver = null,
        IOAuth2TokenProvider? oauth2TokenProvider = null,
        IMLog<ExternalScenarioExecutor>? logger = null)
    {
        _connector = connector;
        _authResolver = authResolver;
        _oauth2TokenProvider = oauth2TokenProvider;
        _logger = logger;
    }

    /// <summary>
    /// Executes the scenario against the external endpoint in config.
    /// Resolves auth credentials before execution. Retries once on 401 for OAuth2 strategy.
    /// </summary>
    public async Task<ScenarioResult> ExecuteAsync(NeuronScenario scenario, ExternalProjectConfig config, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            // Resolve auth credentials before building context
            AuthResult? authResult = _authResolver != null
                ? await _authResolver.ResolveAsync(config, ct)
                : null;

            ConnectorContext context = BuildConnectorContext(scenario, config, authResult);
            ConnectorResult connectorResult = await _connector.ExecuteAsync(context, ct);

            // 401 retry logic for OAuth2: invalidate token cache and retry once
            if (IsUnauthorized(connectorResult) &&
                config.AuthStrategy == AuthStrategy.OAuth2ClientCredentials &&
                _authResolver != null &&
                config.OAuth2 != null)
            {
                _logger?.Warn("Received 401 for OAuth2 strategy on project {ProjectId} — invalidating token and retrying",
                    config.ProjectId);

                // Invalidate the cached token so re-resolve fetches a fresh one
                _oauth2TokenProvider?.InvalidateToken(config.OAuth2);

                // Re-resolve to get a fresh token
                authResult = await _authResolver.ResolveAsync(config, ct);
                context = BuildConnectorContext(scenario, config, authResult);
                connectorResult = await _connector.ExecuteAsync(context, ct);
            }

            sw.Stop();
            return MapToScenarioResult(scenario.Id, connectorResult, config.ResponseMapping, sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.Error(ex, "External execution for scenario {ScenarioId} failed: {Message}", scenario.Id, ex.Message);
            return new ScenarioResult
            {
                ScenarioId = scenario.Id,
                IsSuccess = false,
                MatchesExpectation = false,
                ActualBehavior = $"error: {ex.Message}",
                Errors = [ex.Message],
                Duration = sw.Elapsed,
                ExecutedAt = DateTimeOffset.UtcNow
            };
        }
    }

    /// <summary>
    /// Determines if the connector result represents a 401 Unauthorized response.
    /// Checks the ErrorMessage for HTTP status indicators.
    /// </summary>
    private static bool IsUnauthorized(ConnectorResult result)
    {
        if (result.Success) return false;
        return result.ErrorMessage?.Contains("401", StringComparison.Ordinal) == true ||
               result.ErrorMessage?.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static ConnectorContext BuildConnectorContext(
        NeuronScenario scenario,
        ExternalProjectConfig config,
        AuthResult? authResult)
    {
        // Merge headers: config.ExecutionHeaders are the base, auth headers take precedence
        Dictionary<string, string> mergedHeaders = [];

        if (config.ExecutionHeaders is { Count: > 0 })
        {
            foreach ((string key, string value) in config.ExecutionHeaders)
                mergedHeaders[key] = value;
        }

        if (authResult?.Headers is { Count: > 0 })
        {
            foreach ((string key, string value) in authResult.Headers)
                mergedHeaders[key] = value; // auth headers override base headers
        }

        // Build config JSON for HttpConnector:
        // url, method POST, body = scenario InputFacts as JSON string, headers, timeout
        var configObj = new Dictionary<string, object?>
        {
            ["url"] = config.ExecutionEndpointUrl,
            ["method"] = "POST",
            ["body"] = scenario.InputFacts.GetRawText(),
            ["timeout"] = config.TimeoutSeconds,
            ["contentType"] = "application/json"
        };

        if (mergedHeaders.Count > 0)
        {
            configObj["headers"] = mergedHeaders;
        }

        string configJson = JsonSerializer.Serialize(configObj, JsonOptions);
        JsonDocument configDoc = JsonDocument.Parse(configJson);

        // Build FactBag from scenario InputFacts
        FactBag factBag = new();
        if (scenario.InputFacts.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in scenario.InputFacts.EnumerateObject())
            {
                factBag[property.Name] = ExtractJsonValue(property.Value);
            }
        }

        // Credentials = merged headers (auth headers take precedence over base headers)
        IReadOnlyDictionary<string, string> credentials = mergedHeaders.Count > 0
            ? mergedHeaders
            : (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();

        return new ConnectorContext
        {
            Config = configDoc,
            InputFacts = factBag,
            Credentials = credentials,
            TenantId = scenario.TenantId,
            CorrelationId = scenario.Id
        };
    }

    private static ScenarioResult MapToScenarioResult(
        string scenarioId,
        ConnectorResult connectorResult,
        ResponseMapping? responseMapping,
        TimeSpan duration)
    {
        bool isSuccess;
        JsonElement? outputFacts;
        List<string> errors = [];

        if (responseMapping != null && !string.IsNullOrWhiteSpace(responseMapping.SuccessField))
        {
            // Extract success indicator from custom JSON path in OutputFacts
            object? successValue = ExtractDotPath(connectorResult.OutputFacts, responseMapping.SuccessField);
            string successStr = successValue?.ToString() ?? string.Empty;
            isSuccess = string.Equals(successStr, responseMapping.SuccessValue, StringComparison.OrdinalIgnoreCase);

            // Extract output from configured path or fall back to full OutputFacts
            object? outputData = !string.IsNullOrWhiteSpace(responseMapping.OutputFieldsPath)
                ? ExtractDotPath(connectorResult.OutputFacts, responseMapping.OutputFieldsPath)
                : connectorResult.OutputFacts;

            outputFacts = SerializeToJsonElement(outputData);

            // Extract error from configured path if present
            if (!isSuccess && !string.IsNullOrWhiteSpace(responseMapping.ErrorFieldPath))
            {
                object? errorValue = ExtractDotPath(connectorResult.OutputFacts, responseMapping.ErrorFieldPath);
                if (errorValue != null)
                {
                    errors.Add(errorValue.ToString() ?? "External execution failed");
                }
            }

            if (!isSuccess && errors.Count == 0)
            {
                errors.Add($"External execution returned success={successStr} (expected: {responseMapping.SuccessValue})");
            }
        }
        else
        {
            // No ResponseMapping: fall back to ConnectorResult.Success (HTTP status based)
            isSuccess = connectorResult.Success;
            outputFacts = SerializeToJsonElement(connectorResult.OutputFacts);

            if (!isSuccess && !string.IsNullOrWhiteSpace(connectorResult.ErrorMessage))
            {
                errors.Add(connectorResult.ErrorMessage);
            }
        }

        return new ScenarioResult
        {
            ScenarioId = scenarioId,
            IsSuccess = isSuccess,
            MatchesExpectation = isSuccess,
            ActualBehavior = isSuccess ? "passed" : $"failed: {string.Join("; ", errors)}",
            OutputFacts = outputFacts,
            Errors = errors,
            Duration = duration,
            ExecutedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Navigates a dot-separated path (e.g., "result.data") through nested dictionaries.
    /// Returns null if any segment is not found.
    /// </summary>
    internal static object? ExtractDotPath(Dictionary<string, object?> facts, string dotPath)
    {
        string[] segments = dotPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        object? current = facts;

        foreach (string segment in segments)
        {
            if (current is Dictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(segment, out current))
                    return null;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private static object? ExtractJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l) ? (object)l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }

    private static JsonElement? SerializeToJsonElement(object? data)
    {
        if (data == null) return null;
        try
        {
            string json = JsonSerializer.Serialize(data, JsonOptions);
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}
