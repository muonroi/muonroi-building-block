namespace Muonroi.RuleEngine.Proliferation.Tests;

public class ClaudeProliferationBrainTests
{
    private readonly ProliferationOptions _options = new()
    {
        BrainProvider = "claude",
        ClaudeEndpoint = "https://api.anthropic.com",
        ClaudeApiKey = "test-key",
        ClaudeModel = "claude-haiku-4-5-20251001",
        AiTimeoutSeconds = 10,
        Temperature = 0.7f,
        MaxTokens = 2000,
        MaxScenariosPerRule = 20
    };

    private readonly DefaultPromptBuilder _promptBuilder = new();

    private static HttpClient CreateMockHttpClient(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        Mock<HttpMessageHandler> handler = new();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        return new HttpClient(handler.Object);
    }

    private ClaudeProliferationBrain CreateBrain(HttpClient client)
    {
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("ClaudeProliferation")).Returns(client);
        return new ClaudeProliferationBrain(factory.Object, _options, _promptBuilder);
    }

    [Fact]
    public async Task AnalyzeAsync_ParsesValidResponse()
    {
        string scenarios = """[{"scenario":"Null input test","type":"technical","reason":"null safety","inputFacts":{},"expectedBehavior":"should handle gracefully"}]""";
        string claudeResponse = JsonSerializer.Serialize(new
        {
            content = new[]
            {
                new { type = "text", text = scenarios }
            }
        });

        ClaudeProliferationBrain brain = CreateBrain(CreateMockHttpClient(claudeResponse));

        ProliferationPlan plan = await brain.AnalyzeAsync(
            "TEST_RULE", "{}", null, null,
            new ProliferationContext { RemainingBudget = 5 });

        plan.Scenarios.Should().HaveCount(1);
        plan.Scenarios[0].ScenarioName.Should().Be("Null input test");
        plan.AiModelUsed.Should().Be("claude-haiku-4-5-20251001");
    }

    [Fact]
    public async Task AnalyzeAsync_ZeroBudget_ReturnsEmpty()
    {
        ClaudeProliferationBrain brain = CreateBrain(CreateMockHttpClient("{}"));

        ProliferationPlan plan = await brain.AnalyzeAsync(
            "TEST", "{}", null, null,
            new ProliferationContext { RemainingBudget = 0 });

        plan.Scenarios.Should().BeEmpty();
        plan.GenerationDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task AnalyzeAsync_ServerError_ReturnsSyntheticFallbackScenarios()
    {
        // When server returns error, synthetic fallback guarantees at least 1 scenario
        ClaudeProliferationBrain brain = CreateBrain(
            CreateMockHttpClient("error", HttpStatusCode.InternalServerError));

        ProliferationPlan plan = await brain.AnalyzeAsync(
            "TEST", "{}", null, null,
            new ProliferationContext { RemainingBudget = 5 });

        // Synthetic fallback guarantees >= 1 scenario
        plan.Scenarios.Should().NotBeEmpty();
        plan.Scenarios.Should().AllSatisfy(s =>
            s.ProliferationReason.Should().Contain("Synthetic boundary case"));
    }

    [Fact]
    public async Task AnalyzeAsync_SendsAnthropicHeaders()
    {
        string? capturedApiKey = null;
        string? capturedVersion = null;
        Mock<HttpMessageHandler> handler = new();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedApiKey = req.Headers.TryGetValues("x-api-key", out var vals) ? vals.First() : null;
                capturedVersion = req.Headers.TryGetValues("anthropic-version", out var v) ? v.First() : null;
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { content = new[] { new { type = "text", text = "[]" } } }),
                    Encoding.UTF8, "application/json")
            });

        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("ClaudeProliferation")).Returns(new HttpClient(handler.Object));
        ClaudeProliferationBrain brain = new(factory.Object, _options, _promptBuilder);

        await brain.AnalyzeAsync("TEST", "{}", null, null,
            new ProliferationContext { RemainingBudget = 5 });

        capturedApiKey.Should().Be("test-key");
        capturedVersion.Should().Be("2023-06-01");
    }
}
