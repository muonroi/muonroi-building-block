namespace Muonroi.Integration.Connectors.Tests;

/// <summary>
/// Unit tests for per-preset ListDocumentsAsync implementations.
/// Uses FakeHttpMessageHandler to capture outgoing requests and return canned JSON responses.
/// No Moq — hand-rolled fake per Decision 06-02.
/// </summary>
public class ListDocumentsTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static (HttpConnector connector, FakeHttpMessageHandler handler) MakeConnector(
        HttpResponseMessage response)
    {
        FakeHttpMessageHandler handler = new(response);
        HttpClient client = new(handler);
        FakeHttpClientFactory factory = new(client);
        HttpConnector connector = new(factory, NullLogger<HttpConnector>.Instance);
        return (connector, handler);
    }

    private static ConnectorContext MakeContext(string baseUrl, string authHeader = "Basic dGVzdDp0ZXN0")
    {
        string configJson = $$"""{"url":"{{baseUrl}}","method":"GET"}""";
        return new ConnectorContext
        {
            Config = JsonDocument.Parse(configJson),
            InputFacts = new FactBag(),
            Credentials = new Dictionary<string, string> { ["authorization"] = authHeader },
            TenantId = "tenant-1",
            CorrelationId = "corr-1"
        };
    }

    // ---------------------------------------------------------------------------
    // Jira Cloud tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task JiraCloud_RecentDefault_BuildsOrderByUpdatedJql_NoTextClause()
    {
        // Arrange
        string cannedJson = """
            {
              "issues": [
                {
                  "key": "FCD-42",
                  "fields": {
                    "summary": "Giao hàng FCD",
                    "updated": "2026-06-15T10:00:00.000+0700",
                    "creator": { "displayName": "BA Ngọc" },
                    "project": { "name": "FCD" }
                  }
                }
              ],
              "total": 1,
              "startAt": 0,
              "maxResults": 20
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new JiraCloudPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://org.atlassian.net/rest/api/3/issue");

        // Act — null SearchText = recent-items default
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: null, Scope: null, TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — request URL must contain "ORDER BY updated DESC" and NOT "text ~"
        handler.LastRequestUri.Should().NotBeNull();
        string requestUrl = handler.LastRequestUri!.ToString();
        (requestUrl.Contains("ORDER+BY+updated+DESC") || requestUrl.Contains("ORDER%20BY%20updated%20DESC") || requestUrl.Contains("ORDER BY updated DESC"))
            .Should().BeTrue("recent-default JQL must contain ORDER BY updated DESC");
        requestUrl.Should().NotContain("text+~", "no text clause for recent-default");
        requestUrl.Should().NotContain("text ~", "no text clause for recent-default");

        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeFalse();
        result.Items.Should().HaveCount(1);
        result.Items[0].ExternalId.Should().Be("FCD-42");
        result.Items[0].Title.Should().Be("Giao hàng FCD");
        result.Items[0].Type.Should().Be("issue");
        result.Items[0].Author.Should().Be("BA Ngọc");
    }

    [Fact]
    public async Task JiraCloud_SearchText_BuildsTextJql()
    {
        // Arrange
        string cannedJson = """
            {
              "issues": [
                {
                  "key": "FCD-10",
                  "fields": {
                    "summary": "VGM Check",
                    "updated": "2026-06-14T08:00:00.000+0700",
                    "creator": { "displayName": "Dev A" },
                    "project": { "name": "FCD" }
                  }
                }
              ],
              "total": 1, "startAt": 0, "maxResults": 20
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new JiraCloudPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://org.atlassian.net/rest/api/3/issue");

        // Act — SearchText present
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: "VGM", Scope: null, TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — request must contain "text ~ "VGM"" or URL-encoded equivalent
        handler.LastRequestUri.Should().NotBeNull();
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Match(u => u.Contains("text+%7E") || u.Contains("text ~") || u.Contains("text%20%7E"),
            "text ~ clause must be in the server-built JQL");

        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].ExternalId.Should().Be("FCD-10");
    }

    [Fact]
    public async Task JiraCloud_Scope_PrependsScopeToJql()
    {
        // Arrange
        string cannedJson = """{"issues":[],"total":0,"startAt":0,"maxResults":20}""";
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new JiraCloudPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://org.atlassian.net/rest/api/3/issue");

        // Act — Scope present
        await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: null, Scope: "MYPROJ", TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — URL must reference "project" AND "MYPROJ" (server-built, not sourced from raw client field)
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Match(u => u.Contains("project") && u.Contains("MYPROJ"),
            "scope/project must be in the server-built JQL");
    }

    [Fact]
    public async Task JiraCloud_ForbiddenResponse_ReturnsPermissionDenied()
    {
        // Arrange — 403 from the remote platform
        HttpResponseMessage response = new(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"errorMessages\":[\"No permission\"]}", Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        IServiceTaskConnector preset = new JiraCloudPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://org.atlassian.net/rest/api/3/issue");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(null, null, null, null),
            CancellationToken.None);

        // Assert — must NOT throw; IsPermissionDenied must be true; Items empty
        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeTrue("403 must set IsPermissionDenied, not throw");
        result.Items.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // Confluence Cloud tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConfluenceCloud_BuildsPageContentQuery_WithSpaceKeyWhenScopeSet()
    {
        // Arrange
        string cannedJson = """
            {
              "results": [
                {
                  "id": "12345",
                  "title": "FCD Giao hàng toàn container",
                  "space": { "key": "FCD" }
                }
              ],
              "_links": {}
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new ConfluencePresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://org.atlassian.net/wiki/rest/api/content");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: null, Scope: "FCD", TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — URL must contain type=page and spaceKey=FCD (server-built)
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Match(u => u.Contains("type") && u.Contains("page"),
            "Confluence browse must request page type");
        requestUrl.Should().Contain("FCD", "space key must be in server-built query");

        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeFalse();
        result.Items.Should().HaveCount(1);
        result.Items[0].ExternalId.Should().Be("12345");
        result.Items[0].Title.Should().Be("FCD Giao hàng toàn container");
        result.Items[0].Type.Should().Be("page");
        result.Items[0].Breadcrumb.Should().Be("FCD");
    }

    [Fact]
    public async Task ConfluenceCloud_ForbiddenResponse_ReturnsPermissionDenied()
    {
        // Arrange
        HttpResponseMessage response = new(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        IServiceTaskConnector preset = new ConfluencePresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://org.atlassian.net/wiki/rest/api/content");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(null, null, null, null),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeTrue();
        result.Items.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // Notion tests
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task Notion_PostsToV1Search_WithPageFilterBody()
    {
        // Arrange
        string cannedJson = """
            {
              "results": [
                {
                  "id": "notion-page-uuid-1",
                  "object": "page",
                  "properties": {
                    "title": {
                      "title": [{ "plain_text": "Tài liệu FCD" }]
                    }
                  }
                }
              ],
              "next_cursor": null,
              "has_more": false
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new NotionPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://api.notion.com/v1/pages/some-page-id", "Bearer secret_token");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: "FCD", Scope: null, TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — must POST to /v1/search
        handler.LastRequestUri.Should().NotBeNull();
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Contain("/v1/search", "Notion browse must POST to /v1/search");
        handler.LastMethod.Should().Be(HttpMethod.Post, "Notion browse uses POST");

        // Assert — request body must contain filter.value=page (server-built)
        handler.LastRequestBody.Should().NotBeNullOrWhiteSpace();
        handler.LastRequestBody.Should().Contain("page", "Notion browse body must have filter value=page");

        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeFalse();
        result.Items.Should().HaveCount(1);
        result.Items[0].ExternalId.Should().Be("notion-page-uuid-1");
        result.Items[0].Title.Should().Be("Tài liệu FCD");
        result.Items[0].Type.Should().Be("page");
    }

    [Fact]
    public async Task Notion_UnauthorizedResponse_ReturnsPermissionDenied()
    {
        // Arrange — 401 from Notion (invalid integration token)
        HttpResponseMessage response = new(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"code\":\"unauthorized\",\"message\":\"API token is invalid.\"}", Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        IServiceTaskConnector preset = new NotionPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://api.notion.com/v1/pages/some-page-id", "Bearer bad_token");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(null, null, null, null),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeTrue("401 must set IsPermissionDenied, not throw");
        result.Items.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // Interface default — GenericRest (not in this plan's 3 presets) should return null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task InterfaceDefault_UnimplementedPreset_ReturnsNull()
    {
        // Arrange — GenericRestPresetConnector uses the default interface method
        (HttpConnector inner, _) = MakeConnector(new HttpResponseMessage(HttpStatusCode.OK));
        IServiceTaskConnector preset = new GenericRestPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://example.com");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(null, null, null, null),
            CancellationToken.None);

        // Assert — null = "browse not supported" honest degrade
        result.Should().BeNull("GenericRest uses the default interface method that returns null");
    }
}

// ---------------------------------------------------------------------------
// Test infrastructure — FakeHttpMessageHandler (no Moq per Decision 06-02)
// ---------------------------------------------------------------------------

internal sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    private readonly HttpResponseMessage _response = response;

    public Uri? LastRequestUri { get; private set; }
    public HttpMethod? LastMethod { get; private set; }
    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        LastMethod = request.Method;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }
        return _response;
    }
}

internal sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    private readonly HttpClient _client = client;

    public HttpClient CreateClient(string name) => _client;
}
