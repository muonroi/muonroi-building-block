namespace Muonroi.Logging.Tests;

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging.Abstractions;
using Moq;
using Xunit;

public class LoggingTests
{
    [Fact]
    public void AddMuonroiLogging_ShouldRegisterRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddMuonroiLogging());
        
        // ISystemExecutionContextAccessor is required by MLog<T>
        services.AddSingleton(Mock.Of<ISystemExecutionContextAccessor>());

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        Assert.NotNull(serviceProvider.GetService<IMLogContext>());
        Assert.NotNull(serviceProvider.GetService<IMLog<LoggingTests>>());
        Assert.NotNull(serviceProvider.GetService<ILogScopeFactory>());
    }

    [Fact]
    public void MLog_Info_ShouldLogWithoutThrowing()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<LoggingTests>>();
        var accessorMock = new Mock<ISystemExecutionContextAccessor>();
        var logContextMock = new Mock<IMLogContext>();

        accessorMock.Setup(x => x.Get()).Returns(new Mock<ISystemExecutionContext>().Object);

        var mlog = new MLog<LoggingTests>(loggerMock.Object, accessorMock.Object, logContextMock.Object);

        // Act & Assert
        var exception = Record.Exception(() => mlog.Info("Test message {param}", "value"));
        Assert.Null(exception);
    }
}
