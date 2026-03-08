namespace Muonroi.AspNetCore.OpenApi.Tests;

using Muonroi.AspNetCore.OpenApi.OpenApi;
using Muonroi.Core.Abstractions.Interfaces;
using Moq;
using Xunit;

public class SwaggerTests
{
    [Fact]
    public void SwaggerDefaultValues_CanBeCreated()
    {
        // Arrange
        var mockJson = new Mock<IMJsonSerializeService>();
        
        // Act
        var filter = new SwaggerDefaultValues(mockJson.Object);

        // Assert
        Assert.NotNull(filter);
    }
}
