namespace Muonroi.Rules.Tests.Feel;

public sealed class FeelApiIntegrationTests
{
    [Fact]
    public async Task EvaluateEndpoint_ShouldHandleOpenClosedRange()
    {
        await using WebApplication app = BuildApp();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/feel/evaluate", new FeelEvalRequest("5 in [1..10)"));
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("result").GetBoolean());
    }

    [Fact]
    public async Task EvaluateEndpoint_ShouldHandleFunctionDefinition()
    {
        await using WebApplication app = BuildApp();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/feel/evaluate",
            new FeelEvalRequest("{add: function(x,y) x + y, result: add(2,3)}.result"));
        response.EnsureSuccessStatusCode();

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(5d, doc.RootElement.GetProperty("result").GetDouble());
    }

    private static WebApplication BuildApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseTestServer();
        WebApplication app = builder.Build();

        app.MapPost("/api/v1/feel/evaluate", (FeelEvalRequest req) =>
        {
            object? result = FeelEvaluator.EvaluateValue(req.Expression, new Dictionary<string, object>());
            return Results.Ok(new { result });
        });

        return app;
    }

    private sealed record FeelEvalRequest(string Expression);
}
