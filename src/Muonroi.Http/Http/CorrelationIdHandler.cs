namespace Muonroi.Http.Http;

/// <summary>
/// Adds correlation and API key headers to outgoing HTTP requests.
/// </summary>
public class CorrelationIdHandler(IAuthenticateInfoContext authContext) : DelegatingHandler
{
    private readonly IAuthenticateInfoContext _authContext = MGuard.NotNull(authContext);

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_authContext.CorrelationId))
        {
            request.Headers.Add(CustomHeader.CorrelationId, _authContext.CorrelationId);
        }

        if (!string.IsNullOrEmpty(_authContext.ApiKey))
        {
            request.Headers.Add(CustomHeader.ApiKey, _authContext.ApiKey);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
