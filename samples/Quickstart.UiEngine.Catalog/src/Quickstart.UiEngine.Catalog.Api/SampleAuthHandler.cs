namespace Quickstart.UiEngine.Catalog.Api;

/// <summary>
/// Sample authentication handler that authenticates every request as a fixed
/// identity. This exists only so the package's [Authorize] catalog controllers
/// are reachable in the quickstart without standing up a real auth provider.
/// Do NOT use in production — replace with JWT / OIDC.
/// </summary>
public sealed class SampleAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims = [new Claim(ClaimTypes.Name, "quickstart-user")];
        ClaimsIdentity identity = new(claims, Scheme.Name);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
