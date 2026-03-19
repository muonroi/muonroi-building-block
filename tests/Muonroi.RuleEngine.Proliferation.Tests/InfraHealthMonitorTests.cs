using System.Net;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Muonroi.RuleEngine.Proliferation.Brain;

namespace Muonroi.RuleEngine.Proliferation.Tests;

public class InfraHealthMonitorTests
{
    private readonly ProliferationOptions _options = new()
    {
        OllamaEndpoint = "http://localhost:11434",
        PrimaryModel = "qwen2.5-coder:14b-instruct-q5_K_M",
        FallbackModel = "qwen2.5-coder:7b-instruct-q5_K_M",
        EnableInfraAwareBudget = true,
        InfraHealthCheckIntervalSeconds = 60,
        InfraSlowThresholdMs = 30000,
        InfraFastThresholdMs = 5000
    };

    private static IHttpClientFactory CreateMockFactory(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        int delayMs = 0,
        bool throwException = false)
    {
        Mock<HttpMessageHandler> handler = new();
        var setup = handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        if (throwException)
        {
            setup.ThrowsAsync(new HttpRequestException("Connection refused"));
        }
        else
        {
            setup.Returns(async () =>
            {
                if (delayMs > 0)
                    await Task.Delay(delayMs);
                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent("{\"models\":[]}", Encoding.UTF8, "application/json")
                };
            });
        }

        HttpClient client = new(handler.Object);
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        return factory.Object;
    }

    [Fact]
    public async Task CheckHealthAsync_FastResponse_ReturnsHealthyWithHighMultiplier()
    {
        // <5s response → Healthy with BudgetMultiplier=1.5, PrimaryModel
        IHttpClientFactory factory = CreateMockFactory(delayMs: 50); // 50ms = well under 5s
        InfraHealthMonitor monitor = new(factory, _options);

        InfraHealthStatus status = await monitor.CheckHealthAsync();

        status.Level.Should().Be(InfraHealthLevel.Healthy);
        status.BudgetMultiplier.Should().Be(1.5);
        status.RecommendedModel.Should().Be(_options.PrimaryModel);
    }

    [Fact]
    public async Task CheckHealthAsync_SlowResponse_ReturnsDegradedWithHalvedMultiplier()
    {
        // Simulate > 30s by using options with very low threshold (1ms fast, 2ms slow)
        ProliferationOptions slowOptions = new()
        {
            OllamaEndpoint = "http://localhost:11434",
            PrimaryModel = "primary-model",
            FallbackModel = "fallback-model",
            EnableInfraAwareBudget = true,
            InfraHealthCheckIntervalSeconds = 60,
            InfraSlowThresholdMs = 10,     // 10ms threshold → 50ms response is "slow"
            InfraFastThresholdMs = 5
        };

        IHttpClientFactory factory = CreateMockFactory(delayMs: 50); // 50ms > slow threshold (10ms)
        InfraHealthMonitor monitor = new(factory, slowOptions);

        InfraHealthStatus status = await monitor.CheckHealthAsync();

        status.Level.Should().Be(InfraHealthLevel.Degraded);
        status.BudgetMultiplier.Should().Be(0.5);
        status.RecommendedModel.Should().Be(slowOptions.FallbackModel);
    }

    [Fact]
    public async Task CheckHealthAsync_Unreachable_ReturnsUnavailable()
    {
        // Connection failure → Unavailable with BudgetMultiplier=0.25
        IHttpClientFactory factory = CreateMockFactory(throwException: true);
        InfraHealthMonitor monitor = new(factory, _options);

        InfraHealthStatus status = await monitor.CheckHealthAsync();

        status.Level.Should().Be(InfraHealthLevel.Unavailable);
        status.BudgetMultiplier.Should().Be(0.25);
        status.RecommendedModel.Should().Be(_options.FallbackModel);
    }

    [Fact]
    public async Task CheckHealthAsync_FastResponse_RecommendsPrimaryModel()
    {
        // <5s → primary model recommended
        IHttpClientFactory factory = CreateMockFactory(delayMs: 0); // immediate response
        InfraHealthMonitor monitor = new(factory, _options);

        InfraHealthStatus status = await monitor.CheckHealthAsync();

        status.RecommendedModel.Should().Be(_options.PrimaryModel);
    }

    [Fact]
    public async Task CheckHealthAsync_CachesResultForConfiguredInterval()
    {
        // Two calls within cache window should only hit HTTP once
        int callCount = 0;
        Mock<HttpMessageHandler> handler = new();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() =>
            {
                callCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"models\":[]}", Encoding.UTF8, "application/json")
                });
            });

        HttpClient client = new(handler.Object);
        Mock<IHttpClientFactory> factory = new();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        InfraHealthMonitor monitor = new(factory.Object, _options);

        // First call
        await monitor.CheckHealthAsync();
        // Second call within cache window
        await monitor.CheckHealthAsync();

        // Should only have hit HTTP once (second call uses cache)
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task CheckHealthAsync_BetweenThresholds_ReturnsNormalMultiplier()
    {
        // Response between InfraFastThresholdMs and InfraSlowThresholdMs → multiplier=1.0
        ProliferationOptions midOptions = new()
        {
            OllamaEndpoint = "http://localhost:11434",
            PrimaryModel = "primary-model",
            FallbackModel = "fallback-model",
            EnableInfraAwareBudget = true,
            InfraHealthCheckIntervalSeconds = 60,
            InfraSlowThresholdMs = 200,    // >200ms = slow
            InfraFastThresholdMs = 10      // <10ms = fast
        };

        // 50ms response → between 10ms and 200ms → normal
        IHttpClientFactory factory = CreateMockFactory(delayMs: 50);
        InfraHealthMonitor monitor = new(factory, midOptions);

        InfraHealthStatus status = await monitor.CheckHealthAsync();

        status.Level.Should().Be(InfraHealthLevel.Healthy);
        status.BudgetMultiplier.Should().Be(1.0);
        status.RecommendedModel.Should().Be(midOptions.PrimaryModel);
    }
}
