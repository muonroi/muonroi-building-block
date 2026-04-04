namespace Muonroi.Http.Tests;

using System;
using System.Linq;
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
using Muonroi.Core.Abstractions.Exceptions;

public class CorrelationIdHandlerTests
{
    private readonly Mock<IAuthenticateInfoContext> _authContextMock = new();

    [Fact]
    public async Task Should_Add_CorrelationId_Header_When_Present()
    {
        _authContextMock.Setup(x => x.CorrelationId).Returns("corr-123");
        _authContextMock.Setup(x => x.ApiKey).Returns(string.Empty);

        var handler = new CorrelationIdHandler(_authContextMock.Object)
        {
            InnerHandler = new CaptureHandler()
        };

        var client = new HttpClient(handler);
        var response = await client.GetAsync("http://test.local");

        var request = response.RequestMessage!;
        Assert.True(request.Headers.Contains("X-Correlation-Id"));
    }

    [Fact]
    public async Task Should_Add_ApiKey_Header_When_Present()
    {
        _authContextMock.Setup(x => x.CorrelationId).Returns(string.Empty);
        _authContextMock.Setup(x => x.ApiKey).Returns("api-key-123");

        var handler = new CorrelationIdHandler(_authContextMock.Object)
        {
            InnerHandler = new CaptureHandler()
        };

        var client = new HttpClient(handler);
        var response = await client.GetAsync("http://test.local");

        var request = response.RequestMessage!;
        Assert.True(request.Headers.Contains("X-Api-Key"));
    }

    [Fact]
    public async Task Should_Not_Add_Headers_When_Values_Are_Empty()
    {
        _authContextMock.Setup(x => x.CorrelationId).Returns(string.Empty);
        _authContextMock.Setup(x => x.ApiKey).Returns(string.Empty);

        var handler = new CorrelationIdHandler(_authContextMock.Object)
        {
            InnerHandler = new CaptureHandler()
        };

        var client = new HttpClient(handler);
        var response = await client.GetAsync("http://test.local");

        var request = response.RequestMessage!;
        Assert.DoesNotContain(request.Headers, h => h.Key == "X-Correlation-Id");
        Assert.DoesNotContain(request.Headers, h => h.Key == "X-Api-Key");
    }

    [Fact]
    public void Constructor_Should_Throw_On_Null_Context()
    {
        Assert.Throws<MArgumentException>(() => new CorrelationIdHandler(null!));
    }

    [Fact]
    public async Task Should_Not_Add_Bearer_Header_When_Not_Authenticated()
    {
        var loggerMock = new Mock<IMLog<AuthenticateHeaderHandler>>();
        var authContextMock = new Mock<IAuthenticateInfoContext>();
        var configMock = new Mock<IConfiguration>();

        authContextMock.Setup(x => x.IsAuthenticated).Returns(false);

        var handler = new AuthenticateHeaderHandler(loggerMock.Object, authContextMock.Object, configMock.Object)
        {
            InnerHandler = new CaptureHandler()
        };

        var client = new HttpClient(handler);
        var response = await client.GetAsync("http://test.local");

        var request = response.RequestMessage!;
        Assert.Null(request.Headers.Authorization);
    }

    private class CaptureHandler : DelegatingHandler
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
