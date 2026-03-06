using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Constants;

namespace Muonroi.Http.Http;

public class CorrelationIdHandler(IAuthenticateInfoContext authContext) : DelegatingHandler
{
    private readonly IAuthenticateInfoContext _authContext = authContext ?? throw new ArgumentNullException(nameof(authContext));

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
