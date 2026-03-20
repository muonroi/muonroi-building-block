using Moq;
using Muonroi.Logging.Abstractions;
using Muonroi.Resilience.Policies;
using Polly;
using Xunit;

namespace Muonroi.Resilience.Tests;
public class ResilienceTests
{
    [Fact]
    public async Task CreateDefaultPipeline_Retry_ShouldExecuteMultipleTimes()
    {
        // Arrange
        Mock<IMLog<PolicyHandler>> loggerMock = new();
        PolicyHandler handler = new(loggerMock.Object);
        ResiliencePipeline<string> pipeline = handler.CreateDefaultPipeline<string>("TestService");

        int executions = 0;

        // Act
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await pipeline.ExecuteAsync(ct =>
            {
                executions++;

                return ValueTask.FromException<string>(
                    new Exception("Failure"));
            });
        });

        // Assert
        Assert.Equal(4, executions);
    }
}
