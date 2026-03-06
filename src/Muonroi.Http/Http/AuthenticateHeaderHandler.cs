using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Http.Http;

public class AuthenticateHeaderHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    private readonly IAuthenticateInfoContext _authContext;

    public IConfiguration Configuration;

    public AuthenticateHeaderHandler(ILogger<AuthenticateHeaderHandler> logger, IAuthenticateInfoContext authContext,
        IConfiguration configuration)
    {
        _logger = logger;
        _authContext = authContext;
        Configuration = configuration;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_authContext.IsAuthenticated)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authContext.GetAccessToken());
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
