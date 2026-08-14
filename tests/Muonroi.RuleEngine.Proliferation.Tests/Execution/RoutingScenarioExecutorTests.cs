namespace Muonroi.RuleEngine.Proliferation.Tests.Execution;

public class RoutingScenarioExecutorTests
{
    private static NeuronScenario BuildScenario(string? tenantId = "tenant-ext")
    {
        return new NeuronScenario
        {
            Id = "scenario-routing-001",
            SeedRuleCode = "order-validation",
            ScenarioName = "Routing test scenario",
            ProliferationReason = "routing test",
            InputFacts = JsonSerializer.SerializeToElement(new { amount = 50 }),
            TenantId = tenantId
        };
    }

    private static ExternalProjectConfig BuildExternalConfig(string? webhookUrl = null)
    {
        return new ExternalProjectConfig
        {
            ProjectId = "proj-ext",
            TenantId = "tenant-ext",
            ExecutionEndpointUrl = "https://external.example.com/execute",
            WebhookUrl = webhookUrl,
            TimeoutSeconds = 30
        };
    }

    private static ScenarioResult BuildScenarioResult(bool isSuccess = true)
    {
        return new ScenarioResult
        {
            ScenarioId = "scenario-routing-001",
            IsSuccess = isSuccess,
            MatchesExpectation = isSuccess,
            ActualBehavior = isSuccess ? "passed" : "failed",
            Duration = TimeSpan.FromMilliseconds(50),
            ExecutedAt = DateTimeOffset.UtcNow
        };
    }

    // Test 7: Scenario with TenantId matching external project routes to ExternalScenarioExecutor
    [Fact]
    public async Task ExecuteAsync_WhenTenantHasExternalConfig_RoutesToExternalExecutor()
    {
        // Arrange
        var mockConfigProvider = new Mock<IExternalProjectConfigProvider>();
        var mockInternalExecutor = new Mock<IScenarioExecutor>();
        var mockExternalExecutor = new Mock<IExternalScenarioExecutor>();
        var externalConfig = BuildExternalConfig();
        var scenario = BuildScenario("tenant-ext");
        var expectedResult = BuildScenarioResult(true);

        mockConfigProvider
            .Setup(p => p.GetConfigAsync("tenant-ext", It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalConfig);

        mockExternalExecutor
            .Setup(e => e.ExecuteAsync(scenario, externalConfig, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var executor = new RoutingScenarioExecutor(
            mockInternalExecutor.Object,
            mockExternalExecutor.Object,
            mockConfigProvider.Object);

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario);

        // Assert
        result.Should().Be(expectedResult);
        mockExternalExecutor.Verify(e => e.ExecuteAsync(scenario, externalConfig, It.IsAny<CancellationToken>()), Times.Once);
        mockInternalExecutor.Verify(e => e.ExecuteAsync(scenario, It.IsAny<CancellationToken>()), Times.Never);
    }

    // Test 8: Scenario with TenantId NOT matching routes to internal ScenarioExecutor
    [Fact]
    public async Task ExecuteAsync_WhenTenantHasNoExternalConfig_RoutesToInternalExecutor()
    {
        // Arrange
        var mockConfigProvider = new Mock<IExternalProjectConfigProvider>();
        var mockInternalExecutor = new Mock<IScenarioExecutor>();
        var mockExternalExecutor = new Mock<IExternalScenarioExecutor>();
        var scenario = BuildScenario("tenant-internal");
        var expectedResult = BuildScenarioResult(true);

        mockConfigProvider
            .Setup(p => p.GetConfigAsync("tenant-internal", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalProjectConfig?)null);

        mockInternalExecutor
            .Setup(e => e.ExecuteAsync(scenario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var executor = new RoutingScenarioExecutor(
            mockInternalExecutor.Object,
            mockExternalExecutor.Object,
            mockConfigProvider.Object);

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario);

        // Assert
        result.Should().Be(expectedResult);
        mockInternalExecutor.Verify(e => e.ExecuteAsync(scenario, It.IsAny<CancellationToken>()), Times.Once);
        mockExternalExecutor.Verify(e => e.ExecuteAsync(It.IsAny<NeuronScenario>(), It.IsAny<ExternalProjectConfig>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Test 9: Scenario with null TenantId routes to internal ScenarioExecutor
    [Fact]
    public async Task ExecuteAsync_WhenTenantIdIsNull_RoutesToInternalExecutor()
    {
        // Arrange
        var mockConfigProvider = new Mock<IExternalProjectConfigProvider>();
        var mockInternalExecutor = new Mock<IScenarioExecutor>();
        var mockExternalExecutor = new Mock<IExternalScenarioExecutor>();
        var scenario = BuildScenario(null);
        var expectedResult = BuildScenarioResult(true);

        mockConfigProvider
            .Setup(p => p.GetConfigAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalProjectConfig?)null);

        mockInternalExecutor
            .Setup(e => e.ExecuteAsync(scenario, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var executor = new RoutingScenarioExecutor(
            mockInternalExecutor.Object,
            mockExternalExecutor.Object,
            mockConfigProvider.Object);

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario);

        // Assert
        result.Should().Be(expectedResult);
        mockInternalExecutor.Verify(e => e.ExecuteAsync(scenario, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Test 10: After external execution, calls IWebhookNotificationService.NotifyAsync when WebhookUrl is non-null
    [Fact]
    public async Task ExecuteAsync_WhenWebhookUrlIsSet_CallsWebhookNotificationAfterExternalExecution()
    {
        // Arrange
        var mockConfigProvider = new Mock<IExternalProjectConfigProvider>();
        var mockInternalExecutor = new Mock<IScenarioExecutor>();
        var mockExternalExecutor = new Mock<IExternalScenarioExecutor>();
        var mockWebhookService = new Mock<IWebhookNotificationService>();
        var externalConfig = BuildExternalConfig(webhookUrl: "https://webhook.example.com/notify");
        var scenario = BuildScenario("tenant-ext");
        var expectedResult = BuildScenarioResult(true);

        mockConfigProvider
            .Setup(p => p.GetConfigAsync("tenant-ext", It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalConfig);

        mockExternalExecutor
            .Setup(e => e.ExecuteAsync(scenario, externalConfig, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        mockWebhookService
            .Setup(w => w.NotifyAsync(
                "https://webhook.example.com/notify",
                It.IsAny<WebhookPayload>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var executor = new RoutingScenarioExecutor(
            mockInternalExecutor.Object,
            mockExternalExecutor.Object,
            mockConfigProvider.Object,
            mockWebhookService.Object);

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario);

        // Allow brief time for fire-and-forget
        await Task.Delay(100);

        // Assert
        result.Should().Be(expectedResult);
        mockWebhookService.Verify(
            w => w.NotifyAsync("https://webhook.example.com/notify", It.IsAny<WebhookPayload>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Test 11: Skips webhook notification when WebhookUrl is null
    [Fact]
    public async Task ExecuteAsync_WhenWebhookUrlIsNull_SkipsWebhookNotification()
    {
        // Arrange
        var mockConfigProvider = new Mock<IExternalProjectConfigProvider>();
        var mockInternalExecutor = new Mock<IScenarioExecutor>();
        var mockExternalExecutor = new Mock<IExternalScenarioExecutor>();
        var mockWebhookService = new Mock<IWebhookNotificationService>();
        var externalConfig = BuildExternalConfig(webhookUrl: null);
        var scenario = BuildScenario("tenant-ext");
        var expectedResult = BuildScenarioResult(true);

        mockConfigProvider
            .Setup(p => p.GetConfigAsync("tenant-ext", It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalConfig);

        mockExternalExecutor
            .Setup(e => e.ExecuteAsync(scenario, externalConfig, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var executor = new RoutingScenarioExecutor(
            mockInternalExecutor.Object,
            mockExternalExecutor.Object,
            mockConfigProvider.Object,
            mockWebhookService.Object);

        // Act
        ScenarioResult result = await executor.ExecuteAsync(scenario);

        // Assert
        result.Should().Be(expectedResult);
        mockWebhookService.Verify(
            w => w.NotifyAsync(It.IsAny<string>(), It.IsAny<WebhookPayload>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Test 12: Webhook notification failure does not fail the overall execution (fire-and-forget)
    [Fact]
    public async Task ExecuteAsync_WhenWebhookNotificationFails_DoesNotFailExecution()
    {
        // Arrange
        var mockConfigProvider = new Mock<IExternalProjectConfigProvider>();
        var mockInternalExecutor = new Mock<IScenarioExecutor>();
        var mockExternalExecutor = new Mock<IExternalScenarioExecutor>();
        var mockWebhookService = new Mock<IWebhookNotificationService>();
        var externalConfig = BuildExternalConfig(webhookUrl: "https://webhook.example.com/notify");
        var scenario = BuildScenario("tenant-ext");
        var expectedResult = BuildScenarioResult(true);

        mockConfigProvider
            .Setup(p => p.GetConfigAsync("tenant-ext", It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalConfig);

        mockExternalExecutor
            .Setup(e => e.ExecuteAsync(scenario, externalConfig, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Webhook throws an exception
        mockWebhookService
            .Setup(w => w.NotifyAsync(It.IsAny<string>(), It.IsAny<WebhookPayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Webhook endpoint unavailable"));

        var executor = new RoutingScenarioExecutor(
            mockInternalExecutor.Object,
            mockExternalExecutor.Object,
            mockConfigProvider.Object,
            mockWebhookService.Object);

        // Act - should NOT throw despite webhook failure
        Func<Task> act = async () => await executor.ExecuteAsync(scenario);

        // Assert
        await act.Should().NotThrowAsync();
        ScenarioResult result = await executor.ExecuteAsync(scenario);
        result.IsSuccess.Should().BeTrue();
    }
}
