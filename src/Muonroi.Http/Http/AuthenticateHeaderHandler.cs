using Muonroi.Logging.Abstractions;

namespace Muonroi.Http.Http;

public class AuthenticateHeaderHandler : DelegatingHandler
{
    private readonly IMLog<AuthenticateHeaderHandler> _logger;

    private readonly IAuthenticateInfoContext _authContext;

    public IConfiguration Configuration;

    public AuthenticateHeaderHandler(IMLog<AuthenticateHeaderHandler> logger, IAuthenticateInfoContext authContext,
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
