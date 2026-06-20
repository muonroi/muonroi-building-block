using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Http;
using Muonroi.Integration.Connectors.Presets;
using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Integration.Connectors.Tests;

/// <summary>
/// Unit tests for Plan 23-02: ListDocumentsAsync for Jira Server, Confluence Server,
/// Azure DevOps, and GitHub presets — plus the Generic REST honest-degrade proof.
/// Uses the same FakeHttpMessageHandler from ListDocumentsTests.cs (Plan 23-01).
/// </summary>
public class ListDocumentsServerTests
{
    // ---------------------------------------------------------------------------
    // Helpers — identical pattern to ListDocumentsTests.cs (Plan 23-01)
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

    private static ConnectorContext MakeContext(string baseUrl, string authHeader = "Bearer test-pat")
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
    // Jira Server / Data Center
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task JiraServer_RecentDefault_BuildsOrderByUpdatedJql_V2SearchEndpoint()
    {
        // Arrange — canned issue list from Jira Server /rest/api/2/search
        string cannedJson = """
            {
              "issues": [
                {
                  "key": "PROJ-7",
                  "fields": {
                    "summary": "Ship FCD container",
                    "updated": "2026-06-10T09:00:00.000+0700",
                    "creator": { "displayName": "Nguyen Van A" },
                    "project": { "name": "PROJ" }
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
        IServiceTaskConnector preset = new JiraServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://jira.example.com/rest/api/2/issue");

        // Act — null SearchText = recent-items default
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: null, Scope: null, TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — must hit /rest/api/2/search (NOT /rest/api/3/search)
        handler.LastRequestUri.Should().NotBeNull();
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Contain("/rest/api/2/search", "Jira Server uses API v2, not v3");
        (requestUrl.Contains("ORDER+BY+updated+DESC") || requestUrl.Contains("ORDER%20BY%20updated%20DESC") || requestUrl.Contains("ORDER BY updated DESC"))
            .Should().BeTrue("recent-default JQL must contain ORDER BY updated DESC");
        requestUrl.Should().NotContain("text+~", "no text clause for recent-default");

        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeFalse();
        result.Items.Should().HaveCount(1);
        result.Items[0].ExternalId.Should().Be("PROJ-7");
        result.Items[0].Title.Should().Be("Ship FCD container");
        result.Items[0].Type.Should().Be("issue");
        result.Items[0].Author.Should().Be("Nguyen Van A");
        result.Items[0].Breadcrumb.Should().Be("PROJ");
    }

    [Fact]
    public async Task JiraServer_SearchText_BuildsTextJql_V2()
    {
        // Arrange
        string cannedJson = """{"issues":[{"key":"X-1","fields":{"summary":"Test","updated":"2026-01-01T00:00:00.000+0000","creator":{"displayName":"Dev"},"project":{"name":"X"}}}],"total":1,"startAt":0,"maxResults":20}""";
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new JiraServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://jira.example.com/rest/api/2/issue");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: "FCD", Scope: null, TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — must hit v2 search with text ~ clause
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Contain("/rest/api/2/search", "Jira Server uses API v2");
        requestUrl.Should().Match(u => u.Contains("text+%7E") || u.Contains("text ~") || u.Contains("text%20%7E"),
            "text ~ clause must be in the server-built JQL");

        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task JiraServer_ForbiddenResponse_ReturnsPermissionDenied()
    {
        // Arrange — 403 from Jira Server
        HttpResponseMessage response = new(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"errorMessages\":[\"No permission\"]}", Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        IServiceTaskConnector preset = new JiraServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://jira.example.com/rest/api/2/issue");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(null, null, null, null),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeTrue("403 must set IsPermissionDenied, not throw");
        result.Items.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // Confluence Server / Data Center
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConfluenceServer_BuildsPageContentQuery_V2Endpoint()
    {
        // Arrange — canned page list from Confluence Server /rest/api/content
        string cannedJson = """
            {
              "results": [
                {
                  "id": "99001",
                  "title": "FCD Process Specification",
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
        IServiceTaskConnector preset = new ConfluenceServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://confluence.example.com/rest/api/content?expand=body.storage");

        // Act — with scope
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: null, Scope: "FCD", TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — must hit /rest/api/content with type=page and spaceKey
        handler.LastRequestUri.Should().NotBeNull();
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Contain("/rest/api/content", "Confluence Server uses /rest/api/content");
        requestUrl.Should().Match(u => u.Contains("type") && u.Contains("page"),
            "Confluence Server browse must request page type");
        requestUrl.Should().Contain("FCD", "space key must be in server-built query");

        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeFalse();
        result.Items.Should().HaveCount(1);
        result.Items[0].ExternalId.Should().Be("99001");
        result.Items[0].Title.Should().Be("FCD Process Specification");
        result.Items[0].Type.Should().Be("page");
        result.Items[0].Breadcrumb.Should().Be("FCD");
    }

    [Fact]
    public async Task ConfluenceServer_ForbiddenResponse_ReturnsPermissionDenied()
    {
        // Arrange — 403 from Confluence Server
        HttpResponseMessage response = new(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        IServiceTaskConnector preset = new ConfluenceServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://confluence.example.com/rest/api/content?expand=body.storage");

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
    // Azure DevOps
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AzureDevOps_PostsWiql_WithProjectFilter_WhenScopeSet()
    {
        // Arrange — canned WIQL response from Azure DevOps
        string cannedJson = """
            {
              "workItems": [
                { "id": 101, "url": "https://dev.azure.com/org/FCD/_apis/wit/workitems/101" },
                { "id": 102, "url": "https://dev.azure.com/org/FCD/_apis/wit/workitems/102" }
              ],
              "columns": [
                { "referenceName": "System.Id", "name": "ID", "url": "" },
                { "referenceName": "System.Title", "name": "Title", "url": "" }
              ]
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new AzureDevOpsPresetConnector(inner);
        ConnectorContext ctx = MakeContext(
            "https://dev.azure.com/org/FCD/_apis/wit/workitems",
            "Basic OjpwYXQ="); // Basic base64(":pat")

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: null, Scope: "FCD", TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — must POST to /_apis/wit/wiql
        handler.LastRequestUri.Should().NotBeNull();
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Contain("/_apis/wit/wiql", "Azure DevOps browse uses WIQL endpoint");
        handler.LastMethod.Should().Be(HttpMethod.Post, "Azure DevOps WIQL uses POST");

        // Assert — request body must contain WIQL FROM WorkItems + project filter
        handler.LastRequestBody.Should().NotBeNullOrWhiteSpace();
        handler.LastRequestBody.Should().Contain("FROM WorkItems", "WIQL body must have FROM WorkItems clause");
        handler.LastRequestBody.Should().Contain("FCD", "WIQL body must include project scope");

        // Assert — items mapped correctly from workItems array
        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeFalse();
        result.Items.Should().HaveCount(2);
        result.Items[0].ExternalId.Should().Be("101");
        result.Items[0].Type.Should().Be("work-item");
    }

    [Fact]
    public async Task AzureDevOps_SearchTextNull_OmitsContainsClause()
    {
        // Arrange
        string cannedJson = """{"workItems":[],"columns":[]}""";
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new AzureDevOpsPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://dev.azure.com/org/FCD/_apis/wit/workitems");

        // Act — no SearchText
        await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: null, Scope: "FCD", TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — WIQL body must NOT contain CONTAINS when SearchText is null
        handler.LastRequestBody.Should().NotContain("CONTAINS",
            "CONTAINS clause must be omitted when SearchText is null");
    }

    [Fact]
    public async Task AzureDevOps_ForbiddenResponse_ReturnsPermissionDenied()
    {
        // Arrange — 403 from Azure DevOps
        HttpResponseMessage response = new(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"message\":\"TF401019: The Git repository with name or identifier does not exist or you do not have permissions for the operation you are attempting.\"}", Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        IServiceTaskConnector preset = new AzureDevOpsPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://dev.azure.com/org/FCD/_apis/wit/workitems");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(null, null, null, null),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeTrue("403 must set IsPermissionDenied, not throw");
        result.Items.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // GitHub
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GitHub_SearchCode_BuildsRepoScopedQuery()
    {
        // Arrange — canned /search/code response
        string cannedJson = """
            {
              "total_count": 2,
              "incomplete_results": false,
              "items": [
                {
                  "name": "README.md",
                  "path": "docs/README.md",
                  "html_url": "https://github.com/org/repo/blob/main/docs/README.md",
                  "repository": { "full_name": "org/repo" }
                },
                {
                  "name": "fcd-spec.md",
                  "path": "specs/fcd-spec.md",
                  "html_url": "https://github.com/org/repo/blob/main/specs/fcd-spec.md",
                  "repository": { "full_name": "org/repo" }
                }
              ]
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new GitHubPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://api.github.com/repos/org/repo/contents/README.md");

        // Act — with SearchText
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: "FCD", Scope: "org/repo", TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — must GET /search/code
        handler.LastRequestUri.Should().NotBeNull();
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Contain("/search/code", "GitHub browse uses /search/code endpoint");
        handler.LastMethod.Should().Be(HttpMethod.Get, "GitHub search uses GET");

        // Assert — query must contain repo:{owner}/{repo}
        requestUrl.Should().Match(u => u.Contains("repo%3A") || u.Contains("repo:"),
            "GitHub search query must include repo: filter");
        requestUrl.Should().Contain("org", "owner must be in the query");
        requestUrl.Should().Contain("repo", "repo name must be in the query");

        // Assert — items mapped correctly
        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeFalse();
        result.Items.Should().HaveCount(2);
        result.Items[0].ExternalId.Should().Be("docs/README.md");
        result.Items[0].Title.Should().Be("README.md");
        result.Items[0].Type.Should().Be("file");
        result.Items[0].Url.Should().Be("https://github.com/org/repo/blob/main/docs/README.md");
        result.Items[0].Breadcrumb.Should().Be("org/repo");
    }

    [Fact]
    public async Task GitHub_ForbiddenResponse_ReturnsPermissionDenied()
    {
        // Arrange — 403 from GitHub API
        HttpResponseMessage response = new(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"message\":\"API rate limit exceeded\"}", Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        IServiceTaskConnector preset = new GitHubPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://api.github.com/repos/org/repo/contents/README.md");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(null, "org/repo", null, null),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsPermissionDenied.Should().BeTrue("403 must set IsPermissionDenied, not throw");
        result.Items.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // Jira Server — JQL scope filter correctness (gap-fix 260616)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task JiraServer_ScopeOnly_NoSearchText_JqlHasNoAndBeforeOrderBy()
    {
        // Arrange — scope set, no search text; historically produced "project = TTOS AND ORDER BY updated DESC" (invalid)
        string cannedJson = """{"issues":[],"total":0,"startAt":0,"maxResults":20}""";
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new JiraServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://jira.example.com/rest/api/2/issue");

        // Act
        await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: null, Scope: "TTOS", TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — JQL must be: project = "TTOS" ORDER BY updated DESC (no AND before ORDER BY)
        string requestUrl = Uri.UnescapeDataString(handler.LastRequestUri!.ToString());
        requestUrl.Should().Contain("project = \"TTOS\"", "scope must be quoted project filter");
        requestUrl.Should().Contain("ORDER BY updated DESC", "ORDER BY clause must be present");
        requestUrl.Should().NotContain("AND ORDER BY",
            "AND immediately before ORDER BY is invalid JQL — this was the bug");
    }

    [Fact]
    public async Task JiraServer_ScopeAndSearchText_JqlHasScopeAndTextBeforeOrderBy()
    {
        // Arrange — scope + search text; must produce: project = "TTOS" AND text ~ "..." ORDER BY updated DESC
        string cannedJson = """{"issues":[],"total":0,"startAt":0,"maxResults":20}""";
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new JiraServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://jira.example.com/rest/api/2/issue");

        // Act
        await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(SearchText: "delivery", Scope: "TTOS", TypeFilter: null, Cursor: null),
            CancellationToken.None);

        // Assert — must have scope AND text ~ before ORDER BY
        string requestUrl = Uri.UnescapeDataString(handler.LastRequestUri!.ToString());
        requestUrl.Should().Contain("project = \"TTOS\"", "scope clause must be present");
        requestUrl.Should().Contain("text ~", "text search clause must be present");
        requestUrl.Should().Contain("ORDER BY updated DESC", "ORDER BY must be present");
        // The AND that joins scope and text is valid; the ORDER BY must come after both
        int orderByIdx = requestUrl.IndexOf("ORDER BY updated DESC", StringComparison.Ordinal);
        int textIdx = requestUrl.IndexOf("text ~", StringComparison.Ordinal);
        orderByIdx.Should().BeGreaterThan(textIdx, "ORDER BY must appear after the text ~ clause");
    }

    // ---------------------------------------------------------------------------
    // Confluence Server — ListScopesAsync (gap-fix 260616)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConfluenceServer_ListScopesAsync_MapsSortedSpaces()
    {
        // Arrange — canned /rest/api/space response with two spaces
        string cannedJson = """
            {
              "results": [
                { "key": "ZSPACE", "name": "Z Space" },
                { "key": "ASPACE", "name": "A Space" }
              ]
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        ConfluenceServerPresetConnector preset = new(inner);
        ConnectorContext ctx = MakeContext("https://confluence.example.com/rest/api/content?expand=body.storage");

        // Act
        IReadOnlyList<ConnectorScope>? scopes = await preset.ListScopesAsync(ctx, CancellationToken.None);

        // Assert — hits /rest/api/space endpoint
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Contain("/rest/api/space", "must query the spaces endpoint");

        // Assert — maps key/name and sorts alphabetically by label
        scopes.Should().NotBeNull();
        scopes!.Should().HaveCount(2);
        scopes[0].Id.Should().Be("ASPACE", "A Space sorts before Z Space");
        scopes[0].Label.Should().Be("A Space (ASPACE)");
        scopes[1].Id.Should().Be("ZSPACE");
        scopes[1].Label.Should().Be("Z Space (ZSPACE)");
    }

    [Fact]
    public async Task ConfluenceServer_ListScopesAsync_ReturnsEmptyOnPermissionDenied()
    {
        // Arrange — 403 from Confluence space endpoint
        HttpResponseMessage response = new(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        ConfluenceServerPresetConnector preset = new(inner);
        ConnectorContext ctx = MakeContext("https://confluence.example.com/rest/api/content?expand=body.storage");

        // Act
        IReadOnlyList<ConnectorScope>? scopes = await preset.ListScopesAsync(ctx, CancellationToken.None);

        // Assert — empty list (not null) because the preset DOES support scopes; permission just denied
        scopes.Should().NotBeNull("empty list ≠ null — null means 'not supported'; empty means 'no access'");
        scopes!.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfluenceServer_ListScopesAsync_ReturnsNullWhenConfigUrlAbsent()
    {
        // Arrange — context with no "url" key in config
        (HttpConnector inner, _) = MakeConnector(new HttpResponseMessage(HttpStatusCode.OK));
        ConfluenceServerPresetConnector preset = new(inner);
        ConnectorContext ctx = new()
        {
            Config = JsonDocument.Parse("{}"),
            InputFacts = new FactBag(),
            Credentials = new Dictionary<string, string>(),
            TenantId = "tenant-1",
            CorrelationId = "corr-1"
        };

        // Act
        IReadOnlyList<ConnectorScope>? scopes = await preset.ListScopesAsync(ctx, CancellationToken.None);

        // Assert — null = "cannot resolve root URL, scopes cannot be fetched"
        scopes.Should().BeNull("null signals that scopes cannot be resolved without a config url");
    }

    // ---------------------------------------------------------------------------
    // Generic REST — honest degrade: no override → default null (BROWSE-01 gate)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenericRest_ListDocumentsAsync_ReturnsNull_HonestDegrade()
    {
        // Arrange — GenericRestPresetConnector does NOT override ListDocumentsAsync;
        // it inherits the IServiceTaskConnector default which returns null.
        (HttpConnector inner, _) = MakeConnector(new HttpResponseMessage(HttpStatusCode.OK));
        IServiceTaskConnector preset = new GenericRestPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://api.example.com/data");

        // Act
        ConnectorBrowseResult? result = await preset.ListDocumentsAsync(
            ctx,
            new ConnectorBrowseQuery(null, null, null, null),
            CancellationToken.None);

        // Assert — null = "browse not supported" — the honest-degrade gate for BROWSE-01
        result.Should().BeNull("GenericRestPresetConnector must NOT override ListDocumentsAsync; null = browse not supported");
    }
}
