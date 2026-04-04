using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests;

/// <summary>
/// Tests for INaturalLanguageRuleConverter and NaturalLanguageRuleConverter.
/// Uses mocked IHttpClientFactory to avoid actual AI calls.
/// </summary>
public sealed class NaturalLanguageRuleConverterTests
{
    private static ProliferationOptions DefaultOptions() => new()
    {
        OllamaEndpoint = "http://localhost:11434",
        PrimaryModel = "qwen2.5-coder:7b",
        AiTimeoutSeconds = 30
    };

    private sealed class FixedResponseHandler(string response) : HttpMessageHandler
    {
        private readonly string _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string ndjsonLine = JsonSerializer.Serialize(new { response = _response, done = true });
            byte[] bytes = Encoding.UTF8.GetBytes(ndjsonLine + "\n");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            });
        }
    }

    private static IHttpClientFactory CreateMockedHttpFactory(string responseJson)
    {
        HttpClient client = new(new FixedResponseHandler(responseJson));
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factory.Object;
    }

    // -----------------------------------------------------------------
    // Test 1: ConvertAsync with valid description returns result with
    //         inputFields containing "orderAmount" and outputFields
    //         containing "discount" when AI returns a valid flowgraph JSON.
    // -----------------------------------------------------------------
    [Fact]
    public async Task ConvertAsync_ValidDescription_ReturnsSuccessWithFields()
    {
        // Arrange
        string validFlowGraphJson = JsonSerializer.Serialize(new
        {
            nodes = new object[]
            {
                new
                {
                    id = "start",
                    type = "Start",
                    data = new
                    {
                        triggerData = new
                        {
                            inputSchema = new Dictionary<string, string>
                            {
                                { "orderAmount", "number" }
                            }
                        }
                    }
                },
                new
                {
                    id = "rule1",
                    type = "RuleTask",
                    data = new
                    {
                        condition = "input.orderAmount > 1000",
                        outputFields = new object[]
                        {
                            new { path = "discount", dataType = "number" }
                        }
                    }
                }
            },
            edges = new object[] { new { source = "start", target = "rule1" } }
        });

        IHttpClientFactory factory = CreateMockedHttpFactory(validFlowGraphJson);
        NaturalLanguageRuleConverter converter = new(factory, DefaultOptions());

        NlConversionRequest request = new()
        {
            Description = "If order amount > 1000 then apply 10% discount",
            OutputFormat = "flowgraph"
        };

        // Act
        NlConversionResult result = await converter.ConvertAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.RuleSetJson.Should().NotBeNullOrEmpty();
        result.DetectedKind.Should().Be(RuleSetKind.FlowGraph);
        result.ErrorMessage.Should().BeNull();
        result.ExtractedSchema.Should().NotBeNull();

        // Schema should have extracted orderAmount as input field
        result.ExtractedSchema!.InputFields
            .Should().Contain(f => f.Name.Equals("orderAmount", StringComparison.OrdinalIgnoreCase));

        // Schema should have extracted discount as output field
        result.ExtractedSchema.OutputFields
            .Should().Contain(f => f.Name.Equals("discount", StringComparison.OrdinalIgnoreCase));
    }

    // -----------------------------------------------------------------
    // Test 2: ConvertAsync with empty description returns error result.
    // -----------------------------------------------------------------
    [Fact]
    public async Task ConvertAsync_EmptyDescription_ReturnsFailure()
    {
        // Arrange
        IHttpClientFactory factory = CreateMockedHttpFactory("{}");
        NaturalLanguageRuleConverter converter = new(factory, DefaultOptions());

        NlConversionRequest request = new() { Description = "   ", OutputFormat = "flowgraph" };

        // Act
        NlConversionResult result = await converter.ConvertAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.RuleSetJson.Should().BeNull();
    }

    // -----------------------------------------------------------------
    // Test 3: ConvertAsync produces JSON parseable by RuleSetSchemaExtractor.Extract
    //         (for flowgraph kind).
    // -----------------------------------------------------------------
    [Fact]
    public async Task ConvertAsync_ProducesJsonParseableBySchemaExtractor()
    {
        // Arrange
        string validJson = JsonSerializer.Serialize(new
        {
            nodes = new object[]
            {
                new
                {
                    id = "n1",
                    type = "RuleTask",
                    data = new { condition = "input.price > 100" }
                }
            },
            edges = Array.Empty<object>()
        });

        IHttpClientFactory factory = CreateMockedHttpFactory(validJson);
        NaturalLanguageRuleConverter converter = new(factory, DefaultOptions());

        NlConversionRequest request = new()
        {
            Description = "Apply surcharge when price > 100",
            OutputFormat = "flowgraph"
        };

        // Act
        NlConversionResult result = await converter.ConvertAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.RuleSetJson.Should().NotBeNullOrEmpty();

        // Independently parse via RuleSetSchemaExtractor
        RuleSetSchema schema = RuleSetSchemaExtractor.Extract(result.RuleSetJson!, RuleSetKind.FlowGraph);
        schema.Should().NotBeNull();
        schema.InputFields.Should().Contain(f => f.Name == "price");
    }

    // -----------------------------------------------------------------
    // Test 4: Output format "decisiontable" sets DetectedKind to DecisionTable.
    // -----------------------------------------------------------------
    [Fact]
    public async Task ConvertAsync_DecisionTableFormat_ReturnsDecisionTableKind()
    {
        // Arrange
        string decisionTableJson = JsonSerializer.Serialize(new
        {
            inputColumns = new object[]
            {
                new { name = "orderAmount", dataType = "number", isRequired = true }
            },
            outputColumns = new object[]
            {
                new { name = "discount", dataType = "number" }
            },
            rules = new object[]
            {
                new { conditions = new[] { "> 1000" }, actions = new[] { "0.1" } }
            }
        });

        IHttpClientFactory factory = CreateMockedHttpFactory(decisionTableJson);
        NaturalLanguageRuleConverter converter = new(factory, DefaultOptions());

        NlConversionRequest request = new()
        {
            Description = "If order amount > 1000 then apply 10% discount",
            OutputFormat = "decisiontable"
        };

        // Act
        NlConversionResult result = await converter.ConvertAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        result.DetectedKind.Should().Be(RuleSetKind.DecisionTable);
    }

    // -----------------------------------------------------------------
    // Test 5: ConvertAsync returns failure when AI returns invalid JSON
    //         (after retries exhausted).
    // -----------------------------------------------------------------
    [Fact]
    public async Task ConvertAsync_InvalidJsonResponse_ReturnsFailure()
    {
        // Arrange: AI always returns non-JSON text
        IHttpClientFactory factory = CreateMockedHttpFactory("This is not valid JSON at all!");
        NaturalLanguageRuleConverter converter = new(factory, DefaultOptions());

        NlConversionRequest request = new()
        {
            Description = "Apply discount for large orders",
            OutputFormat = "flowgraph"
        };

        // Act
        NlConversionResult result = await converter.ConvertAsync(request);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.RuleSetJson.Should().BeNull();
    }
}
