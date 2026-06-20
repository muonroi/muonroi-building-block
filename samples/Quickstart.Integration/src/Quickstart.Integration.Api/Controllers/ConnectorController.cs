using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Integration.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Quickstart.Integration.Api.Models;

namespace Quickstart.Integration.Api.Controllers;

/// <summary>
/// Demonstrates the full feature surface of Muonroi.Integration.Connectors:
///
///   1. GET  /api/connectors               — list all available connectors via IConnectorRegistry
///   2. GET  /api/connectors/{type}        — get metadata + config schema for a single connector
///   3. POST /api/connectors/{type}/test   — test connectivity for any registered connector
///   4. POST /api/connectors/{type}/execute — execute any connector generically via the registry
///   5. POST /api/connectors/http/execute  — named route showcasing HttpConnector with a live call
///   6. POST /api/connectors/slack/webhook — named route showcasing SlackWebhookConnector
/// </summary>
[ApiController]
[Route("api/connectors")]
[Produces("application/json")]
public sealed class ConnectorController(
    IConnectorRegistry registry,
    IConfiguration configuration,
    ILogger<ConnectorController> logger) : ControllerBase
{
    private readonly IConnectorRegistry _registry = registry;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<ConnectorController> _logger = logger;

    // =========================================================================
    // 1. GET /api/connectors
    //    Lists all connector metadata via IConnectorRegistry.ListAvailable().
    // =========================================================================

    /// <summary>
    /// Lists all connectors registered in the DefaultConnectorRegistry.
    /// Returns display name, category, description, icon, and schema presence for each.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<object>), StatusCodes.Status200OK)]
    public IActionResult GetAll()
    {
        IReadOnlyList<ConnectorMetadata> available = _registry.ListAvailable();

        var summary = available.Select(m => new
        {
            m.Type,
            m.DisplayName,
            m.Category,
            m.Description,
            m.RequiresCredentials,
            m.Format,
            HasFieldSchema = m.FieldSchema is not null,
            FieldCount = m.FieldSchema?.Count ?? 0
        });

        return Ok(new
        {
            Count = available.Count,
            Connectors = summary
        });
    }

    // =========================================================================
    // 2. GET /api/connectors/{type}
    //    Resolves a specific connector and returns its metadata + config schema.
    // =========================================================================

    /// <summary>
    /// Returns the metadata and JSON config schema for the specified connector type.
    /// Use this to discover what fields are required before calling /execute.
    /// </summary>
    /// <param name="type">Connector type key (e.g. "http", "slack", "github").</param>
    [HttpGet("{type}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByType(string type)
    {
        IServiceTaskConnector? connector = _registry.Resolve(type);
        if (connector is null)
            return NotFound(new { Error = $"No connector registered for type '{type}'." });

        ConnectorMetadata meta = connector.Metadata;
        JsonElement schema = connector.GetConfigSchema();

        return Ok(new
        {
            meta.Type,
            meta.DisplayName,
            meta.Category,
            meta.Description,
            meta.RequiresCredentials,
            meta.Format,
            FieldSchema = meta.FieldSchema,
            ConfigSchema = schema
        });
    }

    // =========================================================================
    // 3. POST /api/connectors/{type}/test
    //    Calls TestConnectionAsync on the resolved connector.
    // =========================================================================

    /// <summary>
    /// Tests the connectivity for the specified connector type.
    /// The request body supplies the Config and optional credentials.
    /// </summary>
    /// <remarks>
    /// Example for GitHub:
    /// <code>
    /// POST /api/connectors/github/test
    /// {
    ///   "connectorType": "github",
    ///   "config": { "action": "get-user" },
    ///   "correlationId": "test-001"
    /// }
    /// </code>
    /// Add a "token" credential via appsettings or pass it in credentials.
    /// </remarks>
    [HttpPost("{type}/test")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestConnection(string type, [FromBody] ExecuteConnectorRequest request, CancellationToken ct)
    {
        IServiceTaskConnector? connector = _registry.Resolve(type);
        if (connector is null)
            return NotFound(new { Error = $"No connector registered for type '{type}'." });

        ConnectorContext context = BuildContext(request, type);

        bool ok = await connector.TestConnectionAsync(context, ct);

        _logger.LogInformation(
            "TestConnection [{Type}] correlationId={CorrelationId} result={Result}",
            type, request.CorrelationId, ok);

        return Ok(new
        {
            ConnectorType = type,
            Connected = ok,
            request.CorrelationId
        });
    }

    // =========================================================================
    // 4. POST /api/connectors/{type}/execute
    //    Generic execute — resolves the connector from the registry and runs it.
    // =========================================================================

    /// <summary>
    /// Executes any registered connector generically.
    /// The connector is resolved via IConnectorRegistry using the route {type}.
    /// The Config payload is passed directly as ConnectorContext.Config.
    /// </summary>
    /// <remarks>
    /// Example — run the built-in HTTP connector:
    /// <code>
    /// POST /api/connectors/http/execute
    /// {
    ///   "connectorType": "http",
    ///   "config": {
    ///     "url": "https://jsonplaceholder.typicode.com/todos/1",
    ///     "method": "GET"
    ///   }
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("{type}/execute")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Execute(string type, [FromBody] ExecuteConnectorRequest request, CancellationToken ct)
    {
        IServiceTaskConnector? connector = _registry.Resolve(type);
        if (connector is null)
            return NotFound(new { Error = $"No connector registered for type '{type}'." });

        ConnectorContext context = BuildContext(request, type);

        _logger.LogInformation(
            "Executing connector [{Type}] correlationId={CorrelationId}",
            type, request.CorrelationId);

        ConnectorResult result = await connector.ExecuteAsync(context, ct);

        _logger.LogInformation(
            "Connector [{Type}] finished success={Success} status={Status} duration={Duration}ms",
            type, result.Success, result.StatusCode, result.Duration.TotalMilliseconds);

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                ConnectorType = type,
                result.Success,
                result.ErrorMessage,
                result.StatusCode,
                DurationMs = result.Duration.TotalMilliseconds,
                request.CorrelationId
            });
        }

        return Ok(new
        {
            ConnectorType = type,
            result.Success,
            result.StatusCode,
            DurationMs = result.Duration.TotalMilliseconds,
            result.OutputFacts,
            request.CorrelationId
        });
    }

    // =========================================================================
    // 5. POST /api/connectors/http/execute
    //    Named demonstration of the HttpConnector sending a real GET request.
    //    Uses a hard-coded public API URL from appsettings so no body is required.
    // =========================================================================

    /// <summary>
    /// Demonstrates the built-in HttpConnector by calling the public JSONPlaceholder API.
    /// No request body is required — configuration is read from appsettings "Connectors:Http".
    /// Override the URL, method, headers, and body via optional query parameters.
    /// </summary>
    /// <param name="url">Override the target URL (defaults to appsettings value).</param>
    /// <param name="method">HTTP method (GET, POST, PUT, PATCH, DELETE). Defaults to GET.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("http/execute")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> HttpExecute(
        [FromQuery] string? url,
        [FromQuery] string? method,
        CancellationToken ct)
    {
        IServiceTaskConnector? connector = _registry.Resolve("http");
        if (connector is null)
            return StatusCode(500, new { Error = "HttpConnector not registered." });

        // Build config from appsettings defaults, allowing query param overrides
        string targetUrl = url
            ?? _configuration["Connectors:Http:DefaultUrl"]
            ?? "https://jsonplaceholder.typicode.com/todos/1";

        string httpMethod = method
            ?? _configuration["Connectors:Http:DefaultMethod"]
            ?? "GET";

        int timeout = _configuration.GetValue("Connectors:Http:TimeoutSeconds", 30);

        // Build a JSON config matching the HttpConnector schema
        string configJson = $$"""
        {
            "url": "{{targetUrl}}",
            "method": "{{httpMethod}}",
            "timeout": {{timeout}},
            "responseMapping": {
                "todoId": "id",
                "todoTitle": "title",
                "todoCompleted": "completed"
            }
        }
        """;

        ConnectorContext context = new()
        {
            Config = JsonDocument.Parse(configJson),
            InputFacts = new FactBag(),
            Credentials = new Dictionary<string, string>(),
            TenantId = null,
            CorrelationId = HttpContext.TraceIdentifier
        };

        _logger.LogInformation("HttpConnector demo → {Method} {Url}", httpMethod, targetUrl);

        ConnectorResult result = await connector.ExecuteAsync(context, ct);

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                ConnectorType = "http",
                result.Success,
                result.ErrorMessage,
                result.StatusCode,
                DurationMs = result.Duration.TotalMilliseconds
            });
        }

        return Ok(new
        {
            ConnectorType = "http",
            result.Success,
            result.StatusCode,
            DurationMs = result.Duration.TotalMilliseconds,
            result.OutputFacts,
            TargetUrl = targetUrl,
            HttpMethod = httpMethod
        });
    }

    // =========================================================================
    // 6. POST /api/connectors/slack/webhook
    //    Named demonstration of the SlackWebhookConnector.
    // =========================================================================

    /// <summary>
    /// Demonstrates the built-in SlackWebhookConnector.
    /// Configure "Connectors:Slack:Enable=true" and supply a real webhook URL
    /// in appsettings.json (or environment variable) to send a real message.
    /// When Enable=false the endpoint returns a dry-run response.
    /// </summary>
    /// <param name="text">Message text to send (defaults to a sample message).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("slack/webhook")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> SlackWebhook([FromQuery] string? text, CancellationToken ct)
    {
        bool enabled = _configuration.GetValue("Connectors:Slack:Enable", false);
        string webhookUrl = _configuration["Connectors:Slack:WebhookUrl"] ?? "";
        string channel = _configuration["Connectors:Slack:DefaultChannel"] ?? "#general";
        string message = text ?? $"Hello from Quickstart.Integration at {DateTime.UtcNow:u}";

        if (!enabled)
        {
            // Dry-run: show what would be sent without making a real HTTP call
            return Ok(new
            {
                ConnectorType = "slack",
                DryRun = true,
                Note = "Set Connectors:Slack:Enable=true and supply a real WebhookUrl to send.",
                WouldSend = new { webhookUrl = "(hidden)", text = message, channel }
            });
        }

        IServiceTaskConnector? connector = _registry.Resolve("slack");
        if (connector is null)
            return StatusCode(500, new { Error = "SlackWebhookConnector not registered." });

        string configJson = $$"""{ "text": "{{message}}", "channel": "{{channel}}" }""";

        ConnectorContext context = new()
        {
            Config = JsonDocument.Parse(configJson),
            InputFacts = new FactBag(),
            // Slack connector reads webhookUrl from Credentials first
            Credentials = new Dictionary<string, string> { ["webhookUrl"] = webhookUrl },
            TenantId = null,
            CorrelationId = HttpContext.TraceIdentifier
        };

        _logger.LogInformation("SlackWebhookConnector demo → channel={Channel}", channel);

        ConnectorResult result = await connector.ExecuteAsync(context, ct);

        if (!result.Success)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                ConnectorType = "slack",
                result.Success,
                result.ErrorMessage,
                result.StatusCode,
                DurationMs = result.Duration.TotalMilliseconds
            });
        }

        return Ok(new
        {
            ConnectorType = "slack",
            result.Success,
            result.StatusCode,
            DurationMs = result.Duration.TotalMilliseconds,
            result.OutputFacts,
            Message = message,
            Channel = channel
        });
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Builds a <see cref="ConnectorContext"/> from the incoming request,
    /// merging credentials from appsettings for the matching connector type.
    /// </summary>
    private ConnectorContext BuildContext(ExecuteConnectorRequest request, string resolvedType)
    {
        // Merge connector-type-specific credentials from appsettings.
        // In a real app, these would come from a secrets store or vault.
        Dictionary<string, string> credentials = ResolveCredentials(resolvedType);

        return new ConnectorContext
        {
            Config = request.Config,
            InputFacts = new FactBag(),
            Credentials = credentials,
            TenantId = null,
            CorrelationId = request.CorrelationId ?? HttpContext.TraceIdentifier
        };
    }

    /// <summary>
    /// Resolves known credentials from appsettings by connector type.
    /// Extend this to support a proper secrets vault in production.
    /// </summary>
    private Dictionary<string, string> ResolveCredentials(string type) =>
        type.ToLowerInvariant() switch
        {
            "github" => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["token"] = _configuration["Connectors:GitHub:Token"] ?? ""
            },
            "slack" => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["webhookUrl"] = _configuration["Connectors:Slack:WebhookUrl"] ?? ""
            },
            _ => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
}
