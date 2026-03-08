namespace Muonroi.Resilience.Tests;

using System;
using System.Threading.Tasks;
using Muonroi.Resilience.Policies;
using Muonroi.Logging.Abstractions;
using Moq;
using Xunit;
using Polly;

public class ResilienceTests
{
    [Fact]
    public async Task CreateDefaultPipeline_Retry_ShouldExecuteMultipleTimes()
    {
        // Arrange
        var loggerMock = new Mock<IMLog<PolicyHandler>>();
        var handler = new PolicyHandler(loggerMock.Object);
        var pipeline = handler.CreateDefaultPipeline<string>("TestService");
        
        int executions = 0;

        // Act
        try
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                executions++;
                throw new Exception("Failure");
                return await Task.FromResult("Success");
            });
        }
        catch
        {
            // Expected
        }

        // Assert
        // Initial try + 3 retries = 4
        Assert.Equal(4, executions);
    }
}
