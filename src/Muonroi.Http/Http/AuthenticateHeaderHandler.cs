namespace Muonroi.Http.Http;

/// <summary>
/// Adds bearer authorization headers from the current auth context.
/// </summary>
/// <remarks>
/// Initializes the handler.
/// </remarks>
public class AuthenticateHeaderHandler(IMLog<AuthenticateHeaderHandler> logger, IAuthenticateInfoContext authContext,
    IConfiguration configuration) : DelegatingHandler
{
    private readonly IMLog<AuthenticateHeaderHandler> _logger = logger;

    private readonly IAuthenticateInfoContext _authContext = authContext;

    /// <summary>
    /// Configuration used by the handler.
    /// </summary>
    public IConfiguration Configuration = configuration;

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
