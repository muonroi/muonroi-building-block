namespace Muonroi.Experience.Runtime.Tests;

[Trait("Category", "Extraction")]
public sealed class ExperienceBrainTests
{
    private static ExperienceBrainOptions MakeOptions() => new()
    {
        ClaudeEndpoint = "https://api.anthropic.com",
        ClaudeApiKey = "test-key",
        ClaudeModel = "claude-haiku-4-5-20251001",
        OllamaEndpoint = "http://127.0.0.1:11434",
        OllamaPrimaryModel = "qwen2.5-coder:14b-instruct-q5_K_M",
        AiTimeoutSeconds = 30,
        MaxTokens = 800,
        Temperature = 0.3f
    };

    private static IHttpClientFactory MakeFactory(HttpMessageHandler handler, string clientName)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = null };
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(clientName)).Returns(httpClient);
        return factoryMock.Object;
    }

    // -------------------------------------------------------------------------
    // ClaudeExperienceBrain Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ClaudeExperienceBrain_ValidResponse_ParsesNeuronExperience()
    {
        // Arrange: Claude Messages API JSON with NeuronExperience JSON in content[0].text
        const string innerJson = """{"trigger":"retry loop on Edit","question":"why did the edit fail?","reasoning":["file was locked"],"solution":"check file lock first"}""";
        string claudeResponse = $$"""{"content":[{"type":"text","text":{{System.Text.Json.JsonSerializer.Serialize(innerJson)}}}],"model":"claude-haiku-4-5-20251001","stop_reason":"end_turn"}""";

        var handler = new FakeHttpMessageHandler(claudeResponse, HttpStatusCode.OK);
        var factory = MakeFactory(handler, "ClaudeExperienceBrain");
        var brain = new ClaudeExperienceBrain(factory, MakeOptions());

        // Act
        IEnumerable<NeuronExperience> result = await brain.ExtractAsync("test session log");

        // Assert
        result.Should().ContainSingle();
        NeuronExperience exp = result.Single();
        exp.Trigger.Should().Be("retry loop on Edit");
        exp.Question.Should().Be("why did the edit fail?");
        exp.Reasoning.Should().Equal("file was locked");
        exp.Solution.Should().Be("check file lock first");
        exp.CreatedFrom.Should().Be("claude-brain");
        exp.Tier.Should().Be(ExperienceTier.SelfQA);
    }

    [Fact]
    public async Task ClaudeExperienceBrain_MalformedJsonResponse_ReturnsEmpty()
    {
        // Arrange: content[0].text is not valid JSON
        const string claudeResponse = """{"content":[{"type":"text","text":"not valid json at all"}],"model":"claude-haiku-4-5-20251001","stop_reason":"end_turn"}""";

        var handler = new FakeHttpMessageHandler(claudeResponse, HttpStatusCode.OK);
        var factory = MakeFactory(handler, "ClaudeExperienceBrain");
        var brain = new ClaudeExperienceBrain(factory, MakeOptions());

        // Act & Assert
        Func<Task> act = async () =>
        {
            IEnumerable<NeuronExperience> result = await brain.ExtractAsync("test session log");
            result.Should().BeEmpty();
        };
        await act.Should().NotThrowAsync();
    }

    // -------------------------------------------------------------------------
    // OllamaExperienceBrain Tests
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OllamaExperienceBrain_StreamingNdjson_ParsesNeuronExperience()
    {
        // Arrange: streaming NDJSON where accumulated response field is NeuronExperience JSON
        // Split the JSON across two chunks to verify accumulation
        const string part1 = """{"trigger":"test""";
        const string part2 = """ failure","question":"why?","reasoning":["typo"],"solution":"fix typo"}""";

        string ndjson =
            $$"""{"response":{{System.Text.Json.JsonSerializer.Serialize(part1)}},"done":false}""" + "\n" +
            $$"""{"response":{{System.Text.Json.JsonSerializer.Serialize(part2)}},"done":true}""" + "\n";

        var handler = new FakeHttpMessageHandler(ndjson, HttpStatusCode.OK);
        var factory = MakeFactory(handler, "OllamaExperienceBrain");
        var brain = new OllamaExperienceBrain(factory, MakeOptions());

        // Act
        IEnumerable<NeuronExperience> result = await brain.ExtractAsync("test session log");

        // Assert
        result.Should().ContainSingle();
        NeuronExperience exp = result.Single();
        exp.Trigger.Should().Be("test failure");
        exp.Question.Should().Be("why?");
        exp.Reasoning.Should().Equal("typo");
        exp.Solution.Should().Be("fix typo");
        exp.CreatedFrom.Should().Be("ollama-brain");
        exp.Tier.Should().Be(ExperienceTier.SelfQA);
    }

    [Fact]
    public async Task OllamaExperienceBrain_EmptyResponseField_ReturnsEmpty()
    {
        // Arrange: done=true but response is empty
        const string ndjson = "{\"response\":\"\",\"done\":true}\n";

        var handler = new FakeHttpMessageHandler(ndjson, HttpStatusCode.OK);
        var factory = MakeFactory(handler, "OllamaExperienceBrain");
        var brain = new OllamaExperienceBrain(factory, MakeOptions());

        // Act & Assert
        Func<Task> act = async () =>
        {
            IEnumerable<NeuronExperience> result = await brain.ExtractAsync("test session log");
            result.Should().BeEmpty();
        };
        await act.Should().NotThrowAsync();
    }
}

/// <summary>
/// Simple fake HTTP handler that returns a fixed response body.
/// </summary>
internal sealed class FakeHttpMessageHandler(string responseBody, HttpStatusCode statusCode) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
