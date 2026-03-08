namespace Muonroi.Secrets.Tests;

using Microsoft.Extensions.Configuration;
using Muonroi.Secrets.Secrets;
using Moq;
using Xunit;

public class SecretsTests
{
    [Fact]
    public void ConfigurationSecretProvider_GetSecret_ShouldReturnFromConfig()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(x => x["TestSecret"]).Returns("SecretValue");
        
        var provider = new ConfigurationSecretProvider(configMock.Object);

        // Act
        var secret = provider.GetSecret("TestSecret");

        // Assert
        Assert.Equal("SecretValue", secret);
    }

    [Fact]
    public void ConfigurationSecretProvider_GetSecret_ShouldReturnNullIfNotFound()
    {
        // Arrange
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(x => x["Unknown"]).Returns((string?)null);
        
        var provider = new ConfigurationSecretProvider(configMock.Object);

        // Act
        var secret = provider.GetSecret("Unknown");

        // Assert
        Assert.Null(secret);
    }
}
