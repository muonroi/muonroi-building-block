using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class OllamaProliferationBrainTests
{
    private readonly ProliferationOptions _options = new()
    {
        OllamaEndpoint = "http://localhost:11434",
        PrimaryModel = "test-primary",
        FallbackModel = "test-fallback",
        AiTimeoutSeconds = 10,
        Temperature = 0.7f,
        MaxTokens = 2000,
        MaxScenariosPerRule = 20
    };

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

    private OllamaProliferationBrain CreateBrain(HttpClient client)
    {
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("OllamaProliferation")).Returns(client);
        return new OllamaProliferationBrain(factory.Object, _options);
    }

    [Fact]
    public async Task AnalyzeAsync_ParsesValidResponse()
    {
        string aiScenarios = """
            [
                {
                    "scenario": "Negative order amount",
                    "type": "business",
                    "reason": "Tests negative value boundary",
                    "inputFacts": {"amount": -1, "currency": "USD"},
                    "expectedBehavior": "should fail with validation error"
                },
                {
                    "scenario": "Concurrent rule execution",
                    "type": "technical",
                    "reason": "Tests thread safety under load",
                    "inputFacts": {"amount": 100, "concurrent": true},
                    "expectedBehavior": "should pass"
                }
            ]
            """;
        string ollamaResponse = JsonSerializer.Serialize(new { response = aiScenarios });
        OllamaProliferationBrain brain = CreateBrain(CreateMockHttpClient(ollamaResponse));

        ProliferationPlan plan = await brain.AnalyzeAsync(
            "ORDER_VALIDATE",
            """{"workflowName":"ORDER_VALIDATE","rules":[]}""",
            executionResult: null,
            factBagSnapshot: null,
            new ProliferationContext { RemainingBudget = 10 });

        plan.Scenarios.Should().HaveCount(2);
        plan.Scenarios[0].ScenarioName.Should().Be("Negative order amount");
        plan.Scenarios[0].Type.Should().Be(ScenarioType.Business);
        plan.Scenarios[1].ScenarioName.Should().Be("Concurrent rule execution");
        plan.Scenarios[1].Type.Should().Be(ScenarioType.Technical);
        plan.AiModelUsed.Should().Be("test-primary");
    }

    [Fact]
    public async Task AnalyzeAsync_FallbackModel_OnPrimaryFailure()
    {
        // Primary returns error
        Mock<HttpMessageHandler> handler = new();
        int callCount = 0;
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);

                string scenarios = """[{"scenario":"Fallback test","type":"business","reason":"test","inputFacts":{},"expectedBehavior":"should pass"}]""";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(new { response = scenarios }),
                        Encoding.UTF8, "application/json")
                };
            });

        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient("OllamaProliferation")).Returns(new HttpClient(handler.Object));
        OllamaProliferationBrain brain = new(factory.Object, _options);

        ProliferationPlan plan = await brain.AnalyzeAsync(
            "TEST",
            "{}",
            null, null,
            new ProliferationContext { RemainingBudget = 5 });

        plan.AiModelUsed.Should().Be("test-fallback");
        plan.Scenarios.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidJson_ReturnsEmptyPlan()
    {
        string ollamaResponse = JsonSerializer.Serialize(new { response = "This is not valid JSON at all" });
        OllamaProliferationBrain brain = CreateBrain(CreateMockHttpClient(ollamaResponse));

        ProliferationPlan plan = await brain.AnalyzeAsync(
            "TEST",
            "{}",
            null, null,
            new ProliferationContext { RemainingBudget = 5 });

        plan.Scenarios.Should().BeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_ZeroBudget_ReturnsEmpty()
    {
        OllamaProliferationBrain brain = CreateBrain(CreateMockHttpClient("{}"));

        ProliferationPlan plan = await brain.AnalyzeAsync(
            "TEST",
            "{}",
            null, null,
            new ProliferationContext { RemainingBudget = 0 });

        plan.Scenarios.Should().BeEmpty();
        plan.GenerationDuration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void ParseScenarios_HandlesMarkdownCodeFences()
    {
        string wrapped = """
            ```json
            [{"scenario":"Test","type":"business","reason":"edge case","inputFacts":{},"expectedBehavior":"should pass"}]
            ```
            """;

        IReadOnlyList<NeuronScenario> scenarios = OllamaProliferationBrain.ParseScenarios(
            wrapped,
            "SEED",
            new ProliferationContext { RemainingBudget = 5 });

        scenarios.Should().HaveCount(1);
        scenarios[0].ScenarioName.Should().Be("Test");
    }

    [Fact]
    public void ParseScenarios_SkipsIncompleteEntries()
    {
        string json = """
            [
                {"scenario":"Valid","type":"business","reason":"test","inputFacts":{}},
                {"scenario":"","reason":"missing name"},
                {"type":"technical","inputFacts":{}}
            ]
            """;

        IReadOnlyList<NeuronScenario> scenarios = OllamaProliferationBrain.ParseScenarios(
            json,
            "SEED",
            new ProliferationContext { RemainingBudget = 10 });

        scenarios.Should().HaveCount(1);
        scenarios[0].ScenarioName.Should().Be("Valid");
    }
}
