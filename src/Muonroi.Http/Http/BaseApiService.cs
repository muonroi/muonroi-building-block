namespace Muonroi.Http.Http;

/// <summary>
/// Base API service with resilient HTTP request execution.
/// </summary>
public abstract class BaseApiService(
    IHttpClientFactory httpClientFactory,
    IAuthenticateInfoContext authContext,
    IMLog<BaseApiService> logger)
{
    /// <summary>
    /// HTTP client factory for outbound calls.
    /// </summary>
    protected readonly IHttpClientFactory HttpClientFactory = httpClientFactory;
    /// <summary>
    /// Authentication context used for outgoing requests.
    /// </summary>
    protected readonly IAuthenticateInfoContext AuthContext = authContext;
    /// <summary>
    /// Logger for API operations.
    /// </summary>
    protected readonly IMLog<BaseApiService> Logger = logger;

    /// <summary>
    /// Sends an HTTP request through the provided resilience pipeline.
    /// </summary>
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
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken)
            ?? MGuard.Fail<TResponse>("Response deserialization returned null.");
    }
}
