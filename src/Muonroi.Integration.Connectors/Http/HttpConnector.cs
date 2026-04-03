using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Integration.Abstractions;

namespace Muonroi.Integration.Connectors.Http;

/// <summary>
/// HTTP/REST connector. Supports GET, POST, PUT, DELETE, PATCH.
/// Body can be a Scriban template (rendered before sending).
/// Response is mapped to FactBag via JSONPath-like response mapping.
/// </summary>
public sealed class HttpConnector : IServiceTaskConnector
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Creates an HTTP connector with the provided client factory.
    /// </summary>
    /// <param name="httpClientFactory">Factory used to create HTTP clients.</param>
    public HttpConnector(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Connector metadata describing capabilities and configuration.
    /// </summary>
    public ConnectorMetadata Metadata => new()
    {
        Type = "http",
        DisplayName = "HTTP / REST API",
        Category = "API",
        IconSvg = "<path d=\"M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z\"/>",
        Description = "Send HTTP requests to REST or GraphQL APIs with configurable method, headers, body, and response mapping.",
        RequiresCredentials = false
    };

    /// <summary>
    /// Executes an HTTP request based on the connector configuration.
    /// </summary>
    /// <param name="context">Connector execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The connector result.</returns>
    public async Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();
        JsonElement root = context.Config.RootElement;

        string url = root.GetProperty("url").GetString() ?? throw new MInternalException("url is required");
        string method = root.TryGetProperty("method", out JsonElement m) ? m.GetString() ?? "GET" : "GET";
        int timeout = root.TryGetProperty("timeout", out JsonElement t) ? t.GetInt32() : 30;

        HttpClient client = _httpClientFactory.CreateClient("MuonroiConnector");
        client.Timeout = TimeSpan.FromSeconds(timeout);

        // Headers
        if (root.TryGetProperty("headers", out JsonElement headers))
        {
            foreach (JsonProperty header in headers.EnumerateObject())
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Name, header.Value.GetString());
            }
        }

        // Auth from credentials
        if (context.Credentials.TryGetValue("authorization", out string? authHeader))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
        }
        else if (context.Credentials.TryGetValue("apiKey", out string? apiKey) &&
                 context.Credentials.TryGetValue("apiKeyHeader", out string? apiKeyHeader))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(apiKeyHeader, apiKey);
        }

        HttpRequestMessage request = new(new HttpMethod(method), url);

        // Body
        if (root.TryGetProperty("body", out JsonElement body) && body.ValueKind == JsonValueKind.String)
        {
            string bodyContent = body.GetString() ?? string.Empty;
            string contentType = root.TryGetProperty("contentType", out JsonElement ct2)
                ? ct2.GetString() ?? "application/json"
                : "application/json";
            request.Content = new StringContent(bodyContent, Encoding.UTF8, contentType);
        }

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ConnectorResult.Fail($"HTTP request failed: {ex.Message}", duration: sw.Elapsed);
        }

        sw.Stop();
        string responseBody = await response.Content.ReadAsStringAsync(ct);
        int statusCode = (int)response.StatusCode;

        Dictionary<string, object?> outputFacts = new()
        {
            ["httpStatusCode"] = statusCode,
            ["httpResponseBody"] = responseBody,
            ["httpSuccess"] = response.IsSuccessStatusCode
        };

        // Response mapping
        if (root.TryGetProperty("responseMapping", out JsonElement mapping) && response.IsSuccessStatusCode)
        {
            try
            {
                JsonDocument responseDoc = JsonDocument.Parse(responseBody);
                foreach (JsonProperty prop in mapping.EnumerateObject())
                {
                    string jsonPath = prop.Value.GetString() ?? prop.Name;
                    if (responseDoc.RootElement.TryGetProperty(jsonPath, out JsonElement value))
                    {
                        outputFacts[prop.Name] = ConvertJsonElement(value);
                    }
                }
            }
            catch
            {
                // Response is not JSON — skip mapping
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            return ConnectorResult.Fail(
                $"HTTP {statusCode}: {responseBody[..Math.Min(500, responseBody.Length)]}",
                statusCode,
                sw.Elapsed);
        }

        return ConnectorResult.Ok(outputFacts, statusCode, sw.Elapsed);
    }

    /// <summary>
    /// Executes the request as a connectivity test.
    /// </summary>
    /// <param name="context">Connector execution context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the request succeeds.</returns>
    public async Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct)
    {
        try
        {
            ConnectorResult result = await ExecuteAsync(context, ct);
            return result.Success;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the JSON schema used to configure this connector.
    /// </summary>
    /// <returns>Configuration schema.</returns>
    public JsonElement GetConfigSchema()
    {
        string schema = """
        {
            "type": "object",
            "properties": {
                "url": { "type": "string", "format": "uri", "description": "Target URL" },
                "method": { "type": "string", "enum": ["GET", "POST", "PUT", "DELETE", "PATCH"], "default": "GET" },
                "headers": { "type": "object", "additionalProperties": { "type": "string" }, "description": "Custom HTTP headers" },
                "body": { "type": "string", "description": "Request body (supports Scriban template syntax)" },
                "contentType": { "type": "string", "default": "application/json" },
                "responseMapping": { "type": "object", "additionalProperties": { "type": "string" }, "description": "Map response JSON properties to FactBag keys" },
                "timeout": { "type": "integer", "default": 30, "description": "Timeout in seconds" }
            },
            "required": ["url"]
        }
        """;
        return JsonDocument.Parse(schema).RootElement.Clone();
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };
    }
}
