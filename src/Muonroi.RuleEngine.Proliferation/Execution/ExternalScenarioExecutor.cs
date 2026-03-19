using System.Diagnostics;
using System.Text.Json;
using Muonroi.Integration.Abstractions;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Execution;

/// <summary>
/// Executes proliferation scenarios against external project HTTP endpoints
/// via the existing IServiceTaskConnector infrastructure (HttpConnector).
/// Reuses the connector system per architecture decision — no IHttpClientFactory directly.
/// </summary>
public sealed class ExternalScenarioExecutor : IExternalScenarioExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IServiceTaskConnector _connector;
    private readonly IMLog<ExternalScenarioExecutor>? _logger;

    public ExternalScenarioExecutor(IServiceTaskConnector connector, IMLog<ExternalScenarioExecutor>? logger = null)
    {
        _connector = connector;
        _logger = logger;
    }

    /// <summary>
    /// Executes the scenario against the external endpoint in config.
    /// Builds a ConnectorContext from scenario data, calls ExecuteAsync, and maps the result.
    /// </summary>
    public async Task<ScenarioResult> ExecuteAsync(NeuronScenario scenario, ExternalProjectConfig config, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            ConnectorContext context = BuildConnectorContext(scenario, config);
            ConnectorResult connectorResult = await _connector.ExecuteAsync(context, ct);
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

    private static ConnectorContext BuildConnectorContext(NeuronScenario scenario, ExternalProjectConfig config)
    {
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

        if (config.ExecutionHeaders is { Count: > 0 })
        {
            configObj["headers"] = config.ExecutionHeaders;
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

        // Credentials from ExecutionHeaders (key/value pairs passed as connector credentials)
        IReadOnlyDictionary<string, string> credentials = config.ExecutionHeaders
            ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>();

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
