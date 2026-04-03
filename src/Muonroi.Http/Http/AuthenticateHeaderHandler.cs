using Muonroi.Logging.Abstractions;

namespace Muonroi.Http.Http;

/// <summary>
/// Adds bearer authorization headers from the current auth context.
/// </summary>
public class AuthenticateHeaderHandler : DelegatingHandler
{
    private readonly IMLog<AuthenticateHeaderHandler> _logger;

    private readonly IAuthenticateInfoContext _authContext;

    /// <summary>
    /// Configuration used by the handler.
    /// </summary>
    public IConfiguration Configuration;

    /// <summary>
    /// Initializes the handler.
    /// </summary>
    public AuthenticateHeaderHandler(IMLog<AuthenticateHeaderHandler> logger, IAuthenticateInfoContext authContext,
        IConfiguration configuration)
    {
        _logger = logger;
        _authContext = authContext;
        Configuration = configuration;
    }

    /// <inheritdoc/>
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
