namespace Muonroi.Http.Tests;

public class BaseApiServiceTests
{
    [Fact]
    public async Task SendAsync_WithSuccessfulResponse_ShouldDeserializeBody()
    {
        Mock<IHttpClientFactory> httpClientFactory = new();
        Mock<IAuthenticateInfoContext> authContext = new();
        Mock<IMLog<BaseApiService>> logger = new();

        HttpClient httpClient = new(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new SampleResponse { Name = "ok", Count = 2 })
            }));
        httpClientFactory.Setup(x => x.CreateClient("api")).Returns(httpClient);

        TestApiService service = new(httpClientFactory.Object, authContext.Object, logger.Object);

        SampleResponse response = await service.SendForTestAsync("api", new HttpRequestMessage(HttpMethod.Get, "https://example.test"));

        Assert.Equal("ok", response.Name);
        Assert.Equal(2, response.Count);
    }

    [Fact]
    public async Task SendAsync_WithFailedResponse_ShouldThrow()
    {
        Mock<IHttpClientFactory> httpClientFactory = new();
        Mock<IAuthenticateInfoContext> authContext = new();
        Mock<IMLog<BaseApiService>> logger = new();

        HttpClient httpClient = new(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)));
        httpClientFactory.Setup(x => x.CreateClient("api")).Returns(httpClient);

        TestApiService service = new(httpClientFactory.Object, authContext.Object, logger.Object);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.SendForTestAsync("api", new HttpRequestMessage(HttpMethod.Post, "https://example.test")));
    }

    private sealed class TestApiService(
        IHttpClientFactory httpClientFactory,
        IAuthenticateInfoContext authContext,
        IMLog<BaseApiService> logger) : BaseApiService(httpClientFactory, authContext, logger)
    {
        public Task<SampleResponse> SendForTestAsync(string clientName, HttpRequestMessage request)
        {
            return SendAsync<SampleResponse>(clientName, request, ResiliencePipeline<HttpResponseMessage>.Empty);
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class SampleResponse
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
