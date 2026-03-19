using Muonroi.Governance.Authorization;

namespace Muonroi.Governance.Tests;

/// <summary>
/// Tests for MPolicyDecisionService payload format: OpenFGA uses tuple_key, OPA uses input wrapper.
/// </summary>
public class MPolicyDecisionServicePayloadTests
{
    [Fact]
    public async Task EvaluateAsync_OpenFga_SendsTupleKeyPayload()
    {
        // Arrange — capture the request body sent to OpenFGA
        string? capturedBody = null;
        MPolicyDecisionConfigs configs = new()
        {
            Enabled = true,
            Endpoint = "http://localhost:8080",
            Provider = MPolicyDecisionProvider.OpenFga
        };

        MPolicyDecisionService service = new(
            configs,
            new TestHttpClientFactory(new AsyncTestHandler(async (req, _) =>
            {
                capturedBody = req.Content is not null
                    ? await req.Content.ReadAsStringAsync()
                    : null;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"allowed\":true}")
                };
            })));

        MPolicyDecisionRequest request = new()
        {
            UserId = "user:alice",
            Action = "read",
            Resource = "document:report",
            DecisionType = "permission-key"
        };

        // Act
        MPolicyDecisionResult result = await service.EvaluateAsync(request);

        // Assert — response is authoritative allow
        result.IsAuthoritative.Should().BeTrue();
        result.IsAllowed.Should().BeTrue();
        result.DecisionSource.Should().Be("pdp.openfga");

        // Assert — payload uses tuple_key format, NOT input wrapper
        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("tuple_key");
        capturedBody.Should().Contain("user:alice");
        capturedBody.Should().Contain("read");
        capturedBody.Should().Contain("document:report");
        capturedBody.Should().NotContain("\"input\"");
    }

    [Fact]
    public async Task EvaluateAsync_OpenFga_TupleKey_ContainsUserRelationObject()
    {
        // Arrange — verify exact field names expected by OpenFGA /check endpoint
        string? capturedBody = null;
        MPolicyDecisionConfigs configs = new()
        {
            Enabled = true,
            Endpoint = "http://localhost:8080",
            Provider = MPolicyDecisionProvider.OpenFga
        };

        MPolicyDecisionService service = new(
            configs,
            new TestHttpClientFactory(new AsyncTestHandler(async (req, _) =>
            {
                capturedBody = req.Content is not null
                    ? await req.Content.ReadAsStringAsync()
                    : null;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"allowed\":false}")
                };
            })));

        MPolicyDecisionRequest request = new()
        {
            UserId = "user:bob",
            Action = "delete",
            Resource = "document:secret",
            DecisionType = "permission-key"
        };

        // Act
        await service.EvaluateAsync(request);

        // Assert — tuple_key contains user/relation/object fields
        capturedBody.Should().NotBeNull();
        JsonDocument doc = JsonDocument.Parse(capturedBody!);
        doc.RootElement.TryGetProperty("tuple_key", out JsonElement tupleKey).Should().BeTrue();
        tupleKey.TryGetProperty("user", out JsonElement user).Should().BeTrue();
        user.GetString().Should().Be("user:bob");
        tupleKey.TryGetProperty("relation", out JsonElement relation).Should().BeTrue();
        relation.GetString().Should().Be("delete");
        tupleKey.TryGetProperty("object", out JsonElement obj).Should().BeTrue();
        obj.GetString().Should().Be("document:secret");
    }

    [Fact]
    public async Task EvaluateAsync_Opa_SendsInputWrapperPayload()
    {
        // Arrange — OPA should still use { input: <request> } format
        string? capturedBody = null;
        MPolicyDecisionConfigs configs = new()
        {
            Enabled = true,
            Endpoint = "http://localhost:8181",
            Provider = MPolicyDecisionProvider.Opa
        };

        MPolicyDecisionService service = new(
            configs,
            new TestHttpClientFactory(new AsyncTestHandler(async (req, _) =>
            {
                capturedBody = req.Content is not null
                    ? await req.Content.ReadAsStringAsync()
                    : null;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"result\":true}")
                };
            })));

        MPolicyDecisionRequest request = new()
        {
            UserId = "user:carol",
            Action = "write",
            Resource = "document:plan",
            DecisionType = "permission-key"
        };

        // Act
        MPolicyDecisionResult result = await service.EvaluateAsync(request);

        // Assert — response is authoritative allow
        result.IsAuthoritative.Should().BeTrue();
        result.IsAllowed.Should().BeTrue();
        result.DecisionSource.Should().Be("pdp.opa");

        // Assert — OPA payload uses input wrapper, NOT tuple_key
        capturedBody.Should().NotBeNull();
        capturedBody.Should().Contain("\"input\"");
        capturedBody.Should().NotContain("tuple_key");
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost")
            };
        }
    }

    private sealed class AsyncTestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return responder(request, cancellationToken);
        }
    }
}
