namespace Muonroi.Http.Tests;

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Http.Http;
using Muonroi.Logging.Abstractions;
using Moq;
using Xunit;

public class HttpTests
{
    [Fact]
    public async Task AuthenticateHeaderHandler_Authenticated_ShouldInjectBearerToken()
    {
        // Arrange
        var loggerMock = new Mock<IMLog<AuthenticateHeaderHandler>>();
        var authContextMock = new Mock<IAuthenticateInfoContext>();
        var configMock = new Mock<IConfiguration>();

        authContextMock.Setup(x => x.IsAuthenticated).Returns(true);
        authContextMock.Setup(x => x.GetAccessToken()).Returns("test-token");

        var handler = new AuthenticateHeaderHandler(loggerMock.Object, authContextMock.Object, configMock.Object)
        {
            InnerHandler = new MockResponseHandler()
        };

        var client = new HttpClient(handler);

        // Act
        var response = await client.GetAsync("http://test.com");

        // Assert
        // Check the request headers of the sent message
        var request = response.RequestMessage;
        Assert.NotNull(request);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
    }

    private class MockResponseHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request
            });
        }
    }
}
