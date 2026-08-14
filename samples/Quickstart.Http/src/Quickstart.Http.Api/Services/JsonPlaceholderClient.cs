namespace Quickstart.Http.Api.Services;

/// <summary>
/// A typed HTTP client built on Muonroi.Http <see cref="BaseApiService"/>.
/// BaseApiService.SendAsync() executes the request through a Polly v8
/// <see cref="ResiliencePipeline{T}"/> and deserializes the JSON response.
/// </summary>
public sealed class JsonPlaceholderClient(
    IHttpClientFactory httpClientFactory,
    IAuthenticateInfoContext authContext,
    IMLog<BaseApiService> logger)
    : BaseApiService(httpClientFactory, authContext, logger)
{
    /// <summary>Named HttpClient registered in Program.cs (with the Muonroi handlers attached).</summary>
    public const string ClientName = "jsonplaceholder";

    // A minimal retry pipeline — retries transient failures with exponential backoff.
    private static readonly ResiliencePipeline<HttpResponseMessage> Pipeline =
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200)
            })
            .Build();

    /// <summary>
    /// Fetches a post by id through the resilient SendAsync pipeline.
    /// </summary>
    public Task<PostDto> GetPostAsync(int id, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"posts/{id}");
        return SendAsync<PostDto>(ClientName, request, Pipeline, cancellationToken);
    }
}
