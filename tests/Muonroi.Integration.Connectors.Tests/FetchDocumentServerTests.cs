using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Muonroi.Integration.Abstractions;
using Muonroi.Integration.Connectors.Http;
using Muonroi.Integration.Connectors.Presets;

namespace Muonroi.Integration.Connectors.Tests;

/// <summary>
/// Unit tests for Plan fc9: FetchDocumentAsync on Confluence Server, Jira Server,
/// and the default-null honest-degrade proof on a preset that does not override.
/// Uses the same MakeConnector / MakeContext / FakeHttpMessageHandler helpers as
/// ListDocumentsServerTests.cs (Plan 23-02).
/// </summary>
public class FetchDocumentServerTests
{
    // ---------------------------------------------------------------------------
    // Helpers — identical pattern to ListDocumentsServerTests.cs
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
            InputFacts = new Muonroi.RuleEngine.Abstractions.FactBag(),
            Credentials = new Dictionary<string, string> { ["authorization"] = authHeader },
            TenantId = "tenant-1",
            CorrelationId = "corr-1"
        };
    }

    // ---------------------------------------------------------------------------
    // Confluence Server
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ConfluenceServer_FetchDocumentAsync_FallsBackToStorage_WhenNoExportView()
    {
        // Arrange — response has only body.storage (no export_view node) — proves storage fallback path.
        const string cannedJson = """
            {
              "id": "65592",
              "title": "FCD Process Specification",
              "body": {
                "storage": {
                  "value": "<p>hi</p>",
                  "representation": "storage"
                }
              }
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new ConfluenceServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://confluence.example.com/rest/api/content?expand=body.storage");

        // Act
        ConnectorDocumentContent? result = await preset.FetchDocumentAsync(ctx, "65592", CancellationToken.None);

        // Assert — URL must request both export_view and storage expansions
        handler.LastRequestUri.Should().NotBeNull();
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Contain("/rest/api/content/65592", "must fetch the specific page by id");
        requestUrl.Should().Contain("expand=body.export_view", "must request export_view expansion");
        requestUrl.Should().Contain("body.storage", "must request storage expansion as fallback");

        // Body falls back to storage.value when no export_view node is present
        result.Should().NotBeNull();
        result!.Body.Should().Be("<p>hi</p>", "must return storage.value when export_view is absent");
        result.Format.Should().Be("xhtml", "Confluence Server returns XHTML format");
    }

    [Fact]
    public async Task ConfluenceServer_FetchDocumentAsync_PrefersExportView_OverStorage()
    {
        // Arrange — response has BOTH export_view and storage; export_view wins.
        const string cannedJson = """
            {
              "id": "96377660",
              "title": "Macro-Heavy Page",
              "body": {
                "export_view": {
                  "value": "<div>RENDERED PROSE</div>",
                  "representation": "export_view"
                },
                "storage": {
                  "value": "<ac:structured-macro/>",
                  "representation": "storage"
                }
              }
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new ConfluenceServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://confluence.example.com/rest/api/content?expand=body.storage");

        // Act
        ConnectorDocumentContent? result = await preset.FetchDocumentAsync(ctx, "96377660", CancellationToken.None);

        // Assert — export_view wins; storage macro markup is discarded
        handler.LastRequestUri.Should().NotBeNull();
        handler.LastRequestUri!.ToString().Should().Contain("expand=body.export_view",
            "must request rendered export_view expansion");

        result.Should().NotBeNull();
        result!.Body.Should().Be("<div>RENDERED PROSE</div>",
            "export_view.value must be preferred over storage.value");
        result.Format.Should().Be("xhtml");
    }

    [Fact]
    public async Task ConfluenceServer_FetchDocumentAsync_TagsOnlyExportView_FallsBackToStorage()
    {
        // Arrange — export_view.value is tags/whitespace only (macro page 96377660 pattern).
        // body.export_view.value = "<ac:structured-macro ac:name=\"x\"/>" → strips to 0 visible chars.
        // body.storage.value = real content (69 chars on the real page).
        // The ingest path MUST use storage, not return empty content.
        const string cannedJson = """
            {
              "id": "96377660",
              "title": "SNP Macro Page",
              "body": {
                "export_view": {
                  "value": "<ac:structured-macro ac:name=\"include\"><ac:parameter ac:name=\"\"><ri:page ri:content-title=\"x\"/></ac:parameter></ac:structured-macro>",
                  "representation": "export_view"
                },
                "storage": {
                  "value": "<p>Container delivery requirement step 3: verify seal number against manifest.</p>",
                  "representation": "storage"
                }
              }
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        IServiceTaskConnector preset = new ConfluenceServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://confluence.example.com/rest/api/content?expand=body.storage");

        // Act
        ConnectorDocumentContent? result = await preset.FetchDocumentAsync(ctx, "96377660", CancellationToken.None);

        // Assert — must fall back to storage.value because export_view has no visible text after tag-stripping.
        // Before the fix: export_view was non-whitespace (tags), so code used export_view → 0 ingested chars.
        // After the fix: HasVisibleText(export_view) = false → falls back to storage → prose returned.
        result.Should().NotBeNull();
        result!.Body.Should().Be(
            "<p>Container delivery requirement step 3: verify seal number against manifest.</p>",
            "macro-page export_view (tags-only) must fall back to storage.value");
        result.Format.Should().Be("xhtml");
    }

    [Fact]
    public async Task ConfluenceServer_FetchDocumentAsync_EmptyExportView_FallsBackToStorage()
    {
        // Arrange — export_view.value is empty string; must fall back to storage.value.
        const string cannedJson = """
            {
              "id": "99999",
              "title": "Empty Export View Page",
              "body": {
                "export_view": {
                  "value": "",
                  "representation": "export_view"
                },
                "storage": {
                  "value": "<p>storage body</p>",
                  "representation": "storage"
                }
              }
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        IServiceTaskConnector preset = new ConfluenceServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://confluence.example.com/rest/api/content?expand=body.storage");

        // Act
        ConnectorDocumentContent? result = await preset.FetchDocumentAsync(ctx, "99999", CancellationToken.None);

        // Assert — falls back to storage.value when export_view.value is empty/whitespace
        result.Should().NotBeNull();
        result!.Body.Should().Be("<p>storage body</p>",
            "must fall back to storage.value when export_view.value is empty");
        result.Format.Should().Be("xhtml");
    }

    [Fact]
    public async Task ConfluenceServer_FetchDocumentAsync_PermissionDenied_ReturnsEmptyBodyXhtml()
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
        ConnectorDocumentContent? result = await preset.FetchDocumentAsync(ctx, "65592", CancellationToken.None);

        // Assert — must NOT throw; returns empty-body content with xhtml format
        result.Should().NotBeNull("permission-denied must return empty ConnectorDocumentContent, not null");
        result!.Body.Should().BeEmpty("permission-denied must produce empty body");
        result.Format.Should().Be("xhtml");
    }

    [Fact]
    public async Task ConfluenceServer_FetchDocumentAsync_NoConfigUrl_ReturnsNull()
    {
        // Arrange — context with no url key
        (HttpConnector inner, _) = MakeConnector(new HttpResponseMessage(HttpStatusCode.OK));
        IServiceTaskConnector preset = new ConfluenceServerPresetConnector(inner);
        ConnectorContext ctx = new()
        {
            Config = JsonDocument.Parse("{}"),
            InputFacts = new Muonroi.RuleEngine.Abstractions.FactBag(),
            Credentials = new Dictionary<string, string>(),
            TenantId = "tenant-1",
            CorrelationId = "corr-1"
        };

        // Act
        ConnectorDocumentContent? result = await preset.FetchDocumentAsync(ctx, "65592", CancellationToken.None);

        // Assert — null = cannot resolve root URL
        result.Should().BeNull("no config url means the preset cannot build the fetch URL");
    }

    // ---------------------------------------------------------------------------
    // Jira Server
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task JiraServer_FetchDocumentAsync_ExtractsSummaryAndDescription_AndSetsPlainFormat()
    {
        // Arrange — canned Jira Server /rest/api/2/issue/{key}?fields=summary,description response
        const string cannedJson = """
            {
              "key": "TTOS-1",
              "fields": {
                "summary": "VGM weight check rule",
                "description": "When gross weight exceeds 30000 kg, reject the container."
              }
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, FakeHttpMessageHandler handler) = MakeConnector(response);
        IServiceTaskConnector preset = new JiraServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://jira.example.com/rest/api/2/issue");

        // Act
        ConnectorDocumentContent? result = await preset.FetchDocumentAsync(ctx, "TTOS-1", CancellationToken.None);

        // Assert — URL
        handler.LastRequestUri.Should().NotBeNull();
        string requestUrl = handler.LastRequestUri!.ToString();
        requestUrl.Should().Contain("/rest/api/2/issue/TTOS-1", "must fetch the specific issue by key");
        requestUrl.Should().Contain("fields=", "must request specific fields");
        requestUrl.Should().Contain("summary", "summary field must be requested");

        // Assert — content
        result.Should().NotBeNull();
        result!.Body.Should().Contain("VGM weight check rule", "summary must be in the body");
        result.Body.Should().Contain("When gross weight exceeds 30000 kg", "description must be in the body");
        result.Format.Should().Be("plain", "Jira Server description is plain wiki text, not ADF");
    }

    [Fact]
    public async Task JiraServer_FetchDocumentAsync_PermissionDenied_ReturnsEmptyBodyPlain()
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
        ConnectorDocumentContent? result = await preset.FetchDocumentAsync(ctx, "TTOS-1", CancellationToken.None);

        // Assert
        result.Should().NotBeNull("permission-denied must return empty ConnectorDocumentContent, not null");
        result!.Body.Should().BeEmpty();
        result.Format.Should().Be("plain");
    }

    [Fact]
    public async Task JiraServer_FetchDocumentAsync_NullDescription_ReturnsSummaryOnly()
    {
        // Arrange — issue with no description set
        const string cannedJson = """
            {
              "key": "TTOS-2",
              "fields": {
                "summary": "Missing description issue",
                "description": null
              }
            }
            """;
        HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(cannedJson, Encoding.UTF8, "application/json")
        };
        (HttpConnector inner, _) = MakeConnector(response);
        IServiceTaskConnector preset = new JiraServerPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://jira.example.com/rest/api/2/issue");

        // Act
        ConnectorDocumentContent? result = await preset.FetchDocumentAsync(ctx, "TTOS-2", CancellationToken.None);

        // Assert — body must be summary only (no double-newline trailing, no null)
        result.Should().NotBeNull();
        result!.Body.Should().Be("Missing description issue");
        result.Format.Should().Be("plain");
    }

    // ---------------------------------------------------------------------------
    // Default-null honest-degrade: a preset without an override returns null
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GenericRest_FetchDocumentAsync_ReturnsNull_HonestDegrade()
    {
        // Arrange — GenericRestPresetConnector does NOT override FetchDocumentAsync;
        // it inherits the IServiceTaskConnector default which returns null.
        (HttpConnector inner, _) = MakeConnector(new HttpResponseMessage(HttpStatusCode.OK));
        IServiceTaskConnector preset = new GenericRestPresetConnector(inner);
        ConnectorContext ctx = MakeContext("https://api.example.com/data");

        // Act
        ConnectorDocumentContent? result = await preset.FetchDocumentAsync(ctx, "doc-1", CancellationToken.None);

        // Assert — null = "use legacy ExecuteAsync path"
        result.Should().BeNull("GenericRestPresetConnector must NOT override FetchDocumentAsync; null triggers legacy path");
    }
}
