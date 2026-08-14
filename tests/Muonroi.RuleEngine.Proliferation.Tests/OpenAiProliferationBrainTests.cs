namespace Muonroi.RuleEngine.Proliferation.Tests;

public class OpenAiProliferationBrainTests
{
    private readonly ProliferationOptions _options = new()
    {
        BrainProvider = "openai",
        OpenAiEndpoint = "https://api.openai.com",
        OpenAiApiKey = "test-key",
        OpenAiModel = "gpt-4o-mini",
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

    private OpenAiProliferationBrain CreateBrain(HttpClient client)
    {
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("OpenAiProliferation")).Returns(client);
        return new OpenAiProliferationBrain(factory.Object, _options, _promptBuilder);
    }

    [Fact]
    public async Task AnalyzeAsync_ParsesValidResponse()
    {
        string scenarios = """[{"scenario":"Test edge case","type":"business","reason":"boundary test","inputFacts":{"x":0},"expectedBehavior":"should fail"}]""";
        string openAiResponse = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = scenarios } }
            }
        });

        OpenAiProliferationBrain brain = CreateBrain(CreateMockHttpClient(openAiResponse));

        ProliferationPlan plan = await brain.AnalyzeAsync(
            "TEST_RULE", "{}", null, null,
            new ProliferationContext { RemainingBudget = 5 });

        plan.Scenarios.Should().HaveCount(1);
        plan.Scenarios[0].ScenarioName.Should().Be("Test edge case");
        plan.AiModelUsed.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task AnalyzeAsync_ZeroBudget_ReturnsEmpty()
    {
        OpenAiProliferationBrain brain = CreateBrain(CreateMockHttpClient("{}"));

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
        OpenAiProliferationBrain brain = CreateBrain(
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
    public async Task AnalyzeAsync_SendsAuthorizationHeader()
    {
        string? capturedAuth = null;
        Mock<HttpMessageHandler> handler = new();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedAuth = req.Headers.Authorization?.ToString();
            })
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = "[]" } } } }),
                    Encoding.UTF8, "application/json")
            });

        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("OpenAiProliferation")).Returns(new HttpClient(handler.Object));
        OpenAiProliferationBrain brain = new(factory.Object, _options, _promptBuilder);

        await brain.AnalyzeAsync("TEST", "{}", null, null,
            new ProliferationContext { RemainingBudget = 5 });

        capturedAuth.Should().Be("Bearer test-key");
    }
}
