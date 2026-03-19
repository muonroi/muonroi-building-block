using System.Text.Json;
using FluentAssertions;
using Moq;
using Muonroi.Integration.Abstractions;
using Muonroi.RuleEngine.Proliferation.Execution;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Tests.Execution;

public class ExternalScenarioExecutorTests
{
    private static NeuronScenario BuildScenario(string? tenantId = "tenant-1")
    {
        return new NeuronScenario
        {
            Id = "scenario-001",
            SeedRuleCode = "order-validation",
            ScenarioName = "Test scenario",
            ProliferationReason = "boundary test",
            InputFacts = JsonSerializer.SerializeToElement(new { amount = 100, currency = "USD" }),
            TenantId = tenantId
        };
    }

    private static ExternalProjectConfig BuildConfig(ResponseMapping? responseMapping = null)
    {
        return new ExternalProjectConfig
        {
            ProjectId = "proj-001",
            TenantId = "tenant-1",
            ExecutionEndpointUrl = "https://api.example.com/execute",
            ResponseMapping = responseMapping,
            TimeoutSeconds = 30
        };
    }

    // Test 1: ConnectorResult.Success=true maps to ScenarioResult.IsSuccess=true with OutputFacts
    [Fact]
    public async Task ExecuteAsync_WhenConnectorSucceeds_ReturnsSuccessResult()
    {
        // Arrange
        var mockConnector = new Mock<IServiceTaskConnector>();
        var outputFacts = new Dictionary<string, object?> { ["result"] = "ok", ["score"] = 95 };
        mockConnector
            .Setup(c => c.ExecuteAsync(It.IsAny<ConnectorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectorResult.Ok(outputFacts, 200, TimeSpan.FromMilliseconds(150)));

        var executor = new ExternalScenarioExecutor(mockConnector.Object);
        NeuronScenario scenario = BuildScenario();
        ExternalProjectConfig config = BuildConfig();

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ScenarioId.Should().Be("scenario-001");
        result.OutputFacts.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    // Test 2: ConnectorResult.Success=false maps to ScenarioResult.IsSuccess=false with error
    [Fact]
    public async Task ExecuteAsync_WhenConnectorFails_ReturnsFailureResult()
    {
        // Arrange
        var mockConnector = new Mock<IServiceTaskConnector>();
        mockConnector
            .Setup(c => c.ExecuteAsync(It.IsAny<ConnectorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectorResult.Fail("HTTP 500: Internal Server Error", 500, TimeSpan.FromMilliseconds(100)));

        var executor = new ExternalScenarioExecutor(mockConnector.Object);
        NeuronScenario scenario = BuildScenario();
        ExternalProjectConfig config = BuildConfig();

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario, config);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("HTTP 500");
    }

    // Test 3: ResponseMapping extracts success from custom JSON path (nested)
    [Fact]
    public async Task ExecuteAsync_WithResponseMapping_ExtractsSuccessFromNestedPath()
    {
        // Arrange
        var mockConnector = new Mock<IServiceTaskConnector>();
        var outputFacts = new Dictionary<string, object?>
        {
            ["result"] = new Dictionary<string, object?> { ["success"] = true }
        };
        mockConnector
            .Setup(c => c.ExecuteAsync(It.IsAny<ConnectorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectorResult.Ok(outputFacts, 200, TimeSpan.FromMilliseconds(80)));

        var responseMapping = new ResponseMapping
        {
            SuccessField = "result.success",
            SuccessValue = "true"
        };
        var executor = new ExternalScenarioExecutor(mockConnector.Object);
        NeuronScenario scenario = BuildScenario();
        ExternalProjectConfig config = BuildConfig(responseMapping: responseMapping);

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // Test 4: ResponseMapping extracts output fields from custom JSON path
    [Fact]
    public async Task ExecuteAsync_WithResponseMapping_ExtractsOutputFromPath()
    {
        // Arrange
        var mockConnector = new Mock<IServiceTaskConnector>();
        var nestedData = new Dictionary<string, object?> { ["score"] = 99, ["approved"] = true };
        var outputFacts = new Dictionary<string, object?>
        {
            ["result"] = new Dictionary<string, object?> { ["success"] = true, ["data"] = nestedData }
        };
        mockConnector
            .Setup(c => c.ExecuteAsync(It.IsAny<ConnectorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectorResult.Ok(outputFacts, 200, TimeSpan.FromMilliseconds(80)));

        var responseMapping = new ResponseMapping
        {
            SuccessField = "result.success",
            OutputFieldsPath = "result.data"
        };
        var executor = new ExternalScenarioExecutor(mockConnector.Object);
        NeuronScenario scenario = BuildScenario();
        ExternalProjectConfig config = BuildConfig(responseMapping: responseMapping);

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario, config);

        // Assert
        result.OutputFacts.Should().NotBeNull();
        // Output should come from result.data path
        string outputJson = result.OutputFacts!.Value.GetRawText();
        outputJson.Should().NotBeNullOrEmpty();
    }

    // Test 5: No ResponseMapping falls back to ConnectorResult.Success (HTTP status based)
    [Fact]
    public async Task ExecuteAsync_WithoutResponseMapping_FallsBackToConnectorSuccess()
    {
        // Arrange
        var mockConnector = new Mock<IServiceTaskConnector>();
        mockConnector
            .Setup(c => c.ExecuteAsync(It.IsAny<ConnectorContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectorResult.Ok(new Dictionary<string, object?> { ["httpSuccess"] = true }, 200));

        var executor = new ExternalScenarioExecutor(mockConnector.Object);
        NeuronScenario scenario = BuildScenario();
        ExternalProjectConfig config = BuildConfig(responseMapping: null);

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario, config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // Test 6: HttpConnector exception returns IsSuccess=false with error message
    [Fact]
    public async Task ExecuteAsync_WhenConnectorThrowsException_ReturnsFailureWithError()
    {
        // Arrange
        var mockConnector = new Mock<IServiceTaskConnector>();
        mockConnector
            .Setup(c => c.ExecuteAsync(It.IsAny<ConnectorContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var executor = new ExternalScenarioExecutor(mockConnector.Object);
        NeuronScenario scenario = BuildScenario();
        ExternalProjectConfig config = BuildConfig();

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario, config);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("timed out");
    }
}
