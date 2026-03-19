using Muonroi.Logging.Abstractions;

namespace Muonroi.Http.Http;

public abstract class BaseApiService(
    IHttpClientFactory httpClientFactory,
    IAuthenticateInfoContext authContext,
    IMLog<BaseApiService> logger)
{
    protected readonly IHttpClientFactory HttpClientFactory = httpClientFactory;
    protected readonly IAuthenticateInfoContext AuthContext = authContext;
    protected readonly IMLog<BaseApiService> Logger = logger;

    protected async Task<TResponse> SendAsync<TResponse>(
        string clientName,
        HttpRequestMessage request,
        ResiliencePipeline<HttpResponseMessage> pipeline,
        CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await pipeline.ExecuteAsync(async ct =>
        {
            HttpClient client = HttpClientFactory.CreateClient(clientName);
            return await client.SendAsync(request, ct);
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsAsync<TResponse>(cancellationToken);
    }
}
