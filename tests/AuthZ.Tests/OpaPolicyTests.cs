using Muonroi.AuthZ.Policies;
using System.Net;
using System.Text.Json;

namespace Muonroi.AuthZ.Tests;

public class OpaPolicyTests
{
    private static HttpClient CreateMockClient(bool allowed)
    {
        var response = new { result = allowed };
        MockHttpMessageHandler handler = new(JsonSerializer.Serialize(response));
        HttpClient client = new(handler)
        {
            BaseAddress = new Uri("http://localhost:8181")
        };
        return client;
    }

    [Fact]
    public async Task Allows_valid_request()
    {
        HttpClient client = CreateMockClient(true);
        OpaAuthorizationService svc = new(client);
        var input = new
        {
            tenant_id = "tenant1",
            resource = new { tenant_id = "tenant1" },
            scopes = new[] { "read" },
            attributes = new { role = "admin" }
        };
        Assert.True(await svc.AuthorizeAsync(input));
    }

    [Fact]
    public async Task Denies_mismatched_tenant()
    {
        HttpClient client = CreateMockClient(false);
        OpaAuthorizationService svc = new(client);
        var input = new
        {
            tenant_id = "tenant1",
            resource = new { tenant_id = "tenant2" },
            scopes = new[] { "read" },
            attributes = new { role = "admin" }
        };
        Assert.False(await svc.AuthorizeAsync(input));
    }

    [Fact]
    public async Task Denies_on_http_error()
    {
        MockHttpMessageHandler handler = new("", HttpStatusCode.InternalServerError);
        HttpClient client = new(handler) { BaseAddress = new Uri("http://localhost") };
        OpaAuthorizationService svc = new(client);
        Assert.False(await svc.AuthorizeAsync(new { }));
    }

    private class MockHttpMessageHandler(string response, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage result = new()
            {
                StatusCode = statusCode,
                Content = new StringContent(response)
            };
            return Task.FromResult(result);
        }
    }
}