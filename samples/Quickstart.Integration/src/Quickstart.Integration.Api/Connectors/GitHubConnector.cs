using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Muonroi.Integration.Abstractions;
using Muonroi.RuleEngine.Abstractions;

namespace Quickstart.Integration.Api.Connectors;

/// <summary>
/// Custom GitHub API connector — demonstrates how to extend the registry with a
/// domain-specific connector beyond the built-in set.
///
/// Supported operations (controlled by the "action" config field):
///   "get-user"   — GET /user (authenticated user profile)
///   "list-repos" — GET /repos/{owner}/{repo} (single repo details)
///
/// Configuration fields (passed in ConnectorContext.Config):
///   action  : "get-user" | "list-repos"  (default: "get-user")
///   owner   : GitHub username / org       (required for "list-repos")
///   repo    : repository name             (required for "list-repos")
///
/// Credentials (passed in ConnectorContext.Credentials):
///   token   : GitHub Personal Access Token (read:user scope is sufficient)
/// </summary>
public sealed class GitHubConnector(IHttpClientFactory httpClientFactory) : IServiceTaskConnector
{
    private const string BaseUrl = "https://api.github.com";
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;

    // -------------------------------------------------------------------------
    // Metadata
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public ConnectorMetadata Metadata => new()
    {
        Type = "github",
        DisplayName = "GitHub API",
        Category = "DevOps",
        IconSvg = "<path d=\"M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.531 1.032 1.531 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z\"/>",
        Description = "Interact with the GitHub REST API. Supports fetching user profiles and repository information.",
        RequiresCredentials = true,
        FieldSchema =
        [
            new ConnectorFieldDescriptor
            {
                Key = "action",
                Label = "Action",
                FieldType = "text",
                Placeholder = "get-user | list-repos",
                Required = true
            },
            new ConnectorFieldDescriptor
            {
                Key = "owner",
                Label = "Owner / Organisation",
                FieldType = "text",
                Placeholder = "muonroi",
                Required = false
            },
            new ConnectorFieldDescriptor
            {
                Key = "repo",
                Label = "Repository Name",
                FieldType = "text",
                Placeholder = "muonroi-building-block",
                Required = false
            }
        ]
    };

    // -------------------------------------------------------------------------
    // ExecuteAsync
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct)
    {
        Stopwatch sw = Stopwatch.StartNew();

        JsonElement root = context.Config.RootElement;
        string action = root.TryGetProperty("action", out JsonElement a) ? a.GetString() ?? "get-user" : "get-user";

        string? token = context.Credentials.GetValueOrDefault("token");
        if (string.IsNullOrEmpty(token))
        {
            sw.Stop();
            return ConnectorResult.Fail("GitHub PAT is required. Add 'token' to Credentials.", duration: sw.Elapsed);
        }

        HttpClient client = BuildClient(token);

        try
        {
            return action.ToLowerInvariant() switch
            {
                "list-repos" => await GetRepoAsync(client, root, sw, ct),
                _ => await GetUserAsync(client, sw, ct)
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return ConnectorResult.Fail($"GitHub request failed: {ex.Message}", duration: sw.Elapsed);
        }
    }

    // -------------------------------------------------------------------------
    // TestConnectionAsync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies the supplied token by calling GET /user.
    /// Returns true when the API responds with 200 OK.
    /// </summary>
    public async Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct)
    {
        string? token = context.Credentials.GetValueOrDefault("token");
        if (string.IsNullOrEmpty(token)) return false;

        try
        {
            HttpClient client = BuildClient(token);
            HttpResponseMessage response = await client.GetAsync($"{BaseUrl}/user", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // GetConfigSchema
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a JSON Schema document describing all configuration fields
    /// accepted by this connector's Config payload.
    /// </summary>
    public JsonElement GetConfigSchema()
    {
        const string schema = """
        {
            "type": "object",
            "properties": {
                "action": {
                    "type": "string",
                    "enum": ["get-user", "list-repos"],
                    "default": "get-user",
                    "description": "Operation to perform against the GitHub REST API."
                },
                "owner": {
                    "type": "string",
                    "description": "GitHub username or organisation. Required when action is 'list-repos'."
                },
                "repo": {
                    "type": "string",
                    "description": "Repository name. Required when action is 'list-repos'."
                }
            },
            "required": ["action"],
            "if": { "properties": { "action": { "const": "list-repos" } } },
            "then": { "required": ["owner", "repo"] }
        }
        """;

        return JsonDocument.Parse(schema).RootElement.Clone();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static HttpClient BuildClient(string token)
    {
        // NOTE: In production code use IHttpClientFactory; for the sample we
        // create the client here so the controller can call this without
        // needing extra DI wiring.  The factory-based path is shown in
        // GitHubConnector(IHttpClientFactory) above and used by the registry.
        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Quickstart.Integration", "1.0"));
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static async Task<ConnectorResult> GetUserAsync(HttpClient client, Stopwatch sw, CancellationToken ct)
    {
        HttpResponseMessage response = await client.GetAsync($"{BaseUrl}/user", ct);
        sw.Stop();

        string body = await response.Content.ReadAsStringAsync(ct);
        int statusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
            return ConnectorResult.Fail($"GitHub {statusCode}: {body}", statusCode, sw.Elapsed);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        return ConnectorResult.Ok(new()
        {
            ["githubLogin"] = root.TryGetProperty("login", out JsonElement login) ? login.GetString() : null,
            ["githubName"] = root.TryGetProperty("name", out JsonElement name) ? name.GetString() : null,
            ["githubPublicRepos"] = root.TryGetProperty("public_repos", out JsonElement repos) ? repos.GetInt32() : 0,
            ["githubUrl"] = root.TryGetProperty("html_url", out JsonElement url) ? url.GetString() : null,
            ["githubResponseBody"] = body
        }, statusCode, sw.Elapsed);
    }

    private static async Task<ConnectorResult> GetRepoAsync(
        HttpClient client, JsonElement root, Stopwatch sw, CancellationToken ct)
    {
        string owner = root.TryGetProperty("owner", out JsonElement o) ? o.GetString() ?? "" : "";
        string repo = root.TryGetProperty("repo", out JsonElement r) ? r.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
        {
            sw.Stop();
            return ConnectorResult.Fail("'owner' and 'repo' are required for action 'list-repos'.", duration: sw.Elapsed);
        }

        HttpResponseMessage response = await client.GetAsync($"{BaseUrl}/repos/{owner}/{repo}", ct);
        sw.Stop();

        string body = await response.Content.ReadAsStringAsync(ct);
        int statusCode = (int)response.StatusCode;

        if (!response.IsSuccessStatusCode)
            return ConnectorResult.Fail($"GitHub {statusCode}: {body}", statusCode, sw.Elapsed);

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement repoEl = doc.RootElement;

        return ConnectorResult.Ok(new()
        {
            ["githubRepoFullName"] = repoEl.TryGetProperty("full_name", out JsonElement fn) ? fn.GetString() : null,
            ["githubRepoStars"] = repoEl.TryGetProperty("stargazers_count", out JsonElement stars) ? stars.GetInt32() : 0,
            ["githubRepoForks"] = repoEl.TryGetProperty("forks_count", out JsonElement forks) ? forks.GetInt32() : 0,
            ["githubRepoDescription"] = repoEl.TryGetProperty("description", out JsonElement desc) ? desc.GetString() : null,
            ["githubRepoUrl"] = repoEl.TryGetProperty("html_url", out JsonElement htmlUrl) ? htmlUrl.GetString() : null,
            ["githubResponseBody"] = body
        }, statusCode, sw.Elapsed);
    }
}
