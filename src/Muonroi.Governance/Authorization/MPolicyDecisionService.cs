using Muonroi.Logging.Abstractions;
using System.Net.Http.Json;

namespace Muonroi.Governance.Authorization;

/// <summary>
/// Represents the MPolicy Decision Service.
/// </summary>
public sealed class MPolicyDecisionService(
    MPolicyDecisionConfigs configs,
    IHttpClientFactory httpClientFactory,
    IMLog<MPolicyDecisionService>? logger = null) : IMPolicyDecisionService
{
    private const string ClientName = "MuonroiPolicyDecision";

    private readonly MPolicyDecisionConfigs _configs = configs ?? throw new ArgumentNullException(nameof(configs));
    private readonly IHttpClientFactory _httpClientFactory =
        httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    private readonly IMLog<MPolicyDecisionService>? _logger = logger;

    /// <summary>
    /// Gets the Is Enabled.
    /// </summary>
    public bool IsEnabled => _configs.Enabled;

    /// <summary>
    /// Executes the Evaluate Async operation.
    /// </summary>
    public async Task<MPolicyDecisionResult> EvaluateAsync(
        MPolicyDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_configs.Enabled)
        {
            MPolicyDecisionResult disabled = MPolicyDecisionResult.LocalFallback("local.disabled");
            LogDecision(disabled, request);
            return disabled;
        }

        if (string.IsNullOrWhiteSpace(_configs.Endpoint))
        {
            string endpointError = "PDP endpoint is missing.";
            MPolicyDecisionResult result = HandleFailure(request, endpointError);
            LogDecision(result, request);
            return result;
        }

        try
        {
            HttpClient client = _httpClientFactory.CreateClient(ClientName);
            string path = ResolveDecisionPath(_configs);
            using HttpResponseMessage httpResponse = await client.PostAsJsonAsync(path, BuildPayload(_configs.Provider, request), cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                string body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
                string statusError = $"PDP returned {(int)httpResponse.StatusCode}: {body}";
                MPolicyDecisionResult failedResult = HandleFailure(request, statusError);
                LogDecision(failedResult, request);
                return failedResult;
            }

            string json = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!TryParseDecision(_configs.Provider, json, out bool allowed, out string? parseError))
            {
                MPolicyDecisionResult parsedFailure = HandleFailure(request, parseError ?? "Unable to parse PDP decision.");
                LogDecision(parsedFailure, request);
                return parsedFailure;
            }

            string source = _configs.Provider == MPolicyDecisionProvider.OpenFga ? "pdp.openfga" : "pdp.opa";
            MPolicyDecisionResult result = allowed ? MPolicyDecisionResult.Allowed(source) : MPolicyDecisionResult.Denied(source);
            LogDecision(result, request);
            return result;
        }
        catch (Exception ex)
        {
            MPolicyDecisionResult exceptionResult = HandleFailure(request, ex.Message);
            LogDecision(exceptionResult, request);
            return exceptionResult;
        }
    }

    private MPolicyDecisionResult HandleFailure(MPolicyDecisionRequest request, string error)
    {
        if (_configs.FailureMode == MPolicyDecisionFailureMode.Deny)
        {
            return MPolicyDecisionResult.Denied("pdp.failure", error);
        }

        return MPolicyDecisionResult.LocalFallback("local.fallback", error);
    }

    private void LogDecision(MPolicyDecisionResult result, MPolicyDecisionRequest request)
    {
        if (!_configs.EnableDecisionLogging)
        {
            return;
        }

        _logger?.LogInformation(
            "[PDP] Source={Source}; Authoritative={Authoritative}; Allowed={Allowed}; Fallback={Fallback}; Tenant={Tenant}; Correlation={Correlation}; Action={Action}; Resource={Resource}; Error={Error}",
            result.DecisionSource,
            result.IsAuthoritative,
            result.IsAllowed,
            result.UsedFallback,
            request.TenantId ?? string.Empty,
            request.CorrelationId ?? string.Empty,
            request.Action ?? string.Empty,
            request.Resource ?? string.Empty,
            result.Error ?? string.Empty);
    }

    private static object BuildPayload(MPolicyDecisionProvider provider, MPolicyDecisionRequest request)
    {
        if (provider == MPolicyDecisionProvider.OpenFga)
        {
            // OpenFGA /check endpoint expects { tuple_key: { user, relation, object } }
            return new
            {
                tuple_key = new
                {
                    user = request.UserId ?? string.Empty,
                    relation = request.Action ?? string.Empty,
                    @object = request.Resource ?? string.Empty
                }
            };
        }

        // OPA /v1/data/authz/allow endpoint expects { input: <request> }
        return new
        {
            input = request
        };
    }

    private static string ResolveDecisionPath(MPolicyDecisionConfigs configs)
    {
        if (!string.IsNullOrWhiteSpace(configs.DecisionPath))
        {
            return configs.DecisionPath.Trim();
        }

        return configs.Provider == MPolicyDecisionProvider.OpenFga
            ? "/check"
            : "/v1/data/authz/allow";
    }

    private static bool TryParseDecision(
        MPolicyDecisionProvider provider,
        string json,
        out bool allowed,
        out string? error)
    {
        allowed = false;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Empty PDP response.";
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // OpenFGA style
            if (root.TryGetProperty("allowed", out JsonElement allowedNode) &&
                (allowedNode.ValueKind == JsonValueKind.True || allowedNode.ValueKind == JsonValueKind.False))
            {
                allowed = allowedNode.GetBoolean();
                return true;
            }

            // OPA style
            if (root.TryGetProperty("result", out JsonElement resultNode))
            {
                if (resultNode.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    allowed = resultNode.GetBoolean();
                    return true;
                }

                if (resultNode.ValueKind == JsonValueKind.Object &&
                    resultNode.TryGetProperty("allow", out JsonElement allowNode) &&
                    allowNode.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    allowed = allowNode.GetBoolean();
                    return true;
                }
            }

            error = $"Unsupported PDP response shape for provider '{provider}'.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
