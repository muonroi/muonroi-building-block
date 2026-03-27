namespace Muonroi.Logging.Abstractions.Tests;

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class LogContextTests
{
    [Fact]
    public void IMLogContextScope_Dispose_ShouldNotThrow()
    {
        // Arrange
        var scopeMock = new Mock<IMLogContextScope>();
        
        // Act & Assert
        var exception = Record.Exception(() => scopeMock.Object.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void IMLog_BeginProperty_ShouldReturnValidScope()
    {
        // Arrange
        var logMock = new Mock<IMLog<string>>();
        var scopeMock = new Mock<IMLogContextScope>();
        logMock.Setup(x => x.BeginProperty(It.IsAny<string>(), It.IsAny<object?>()))
               .Returns(scopeMock.Object);

        // Act
        var scope = logMock.Object.BeginProperty("TestKey", "TestValue");

        // Assert
        Assert.NotNull(scope);
        Assert.Same(scopeMock.Object, scope);
    }
}
