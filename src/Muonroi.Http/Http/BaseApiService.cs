using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Http.Http;
using Polly;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Muonroi.Http.Http;

public abstract class BaseApiService(
    IHttpClientFactory httpClientFactory,
    IAuthenticateInfoContext authContext,
    ILogger<BaseApiService> logger)
{
    protected readonly IHttpClientFactory HttpClientFactory = httpClientFactory;
    protected readonly IAuthenticateInfoContext AuthContext = authContext;
    protected readonly ILogger<BaseApiService> Logger = logger;

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
