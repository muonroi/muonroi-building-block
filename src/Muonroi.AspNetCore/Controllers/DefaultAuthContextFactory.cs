using System.IdentityModel.Tokens.Jwt;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.AspNetCore.Controllers;

/// <inheritdoc />
public class DefaultAuthContextFactory(
    IHttpContextAccessor httpContextAccessor,
    ResourceSetting resourceSetting,
    IConfiguration configuration,
    MDbContext dbContext,
    IAmqpContext? amqpContext = null) : IAuthContextFactory
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly ResourceSetting _resourceSetting = resourceSetting;
    private readonly IConfiguration _configuration = configuration;
    private readonly MDbContext _dbContext = dbContext;
    private readonly IAmqpContext? _amqpContext = amqpContext;

/// <inheritdoc />
    public IAuthenticateInfoContext Create()
    {
        IAuthenticateInfoContext authContext;

        if (_httpContextAccessor.HttpContext != null)
        {
            HttpContext context = _httpContextAccessor.HttpContext;

            // SINGLE SOURCE OF TRUTH: DefaultRefreshTokenValidator.ValidateAsync stores the fully
            // validated auth context (JWT signature verified + refresh-token revocation checked) under
            // the type's full name. If it already ran for this request, reuse that context verbatim —
            // this is the authoritative, signature-validated identity and avoids divergence between the
            // validator and this factory.
            if (context.Items.TryGetValue(MGuard.NotNull(typeof(MAuthenticateInfoContext).FullName), out object? cachedCtx)
                && cachedCtx is MAuthenticateInfoContext validated
                && validated.IsAuthenticated)
            {
                _resourceSetting[ResourceSettingKeys.Lang] = string.IsNullOrWhiteSpace(validated.Language)
                    ? "vi-VN"
                    : validated.Language;
                return validated;
            }

            // FALLBACK (order-independence): the factory can be resolved BEFORE the validator runs
            // (e.g. LicenseMiddleware injects MAuthenticateInfoContext earlier in the pipeline). Because
            // this context is scoped/cached, an early resolution would otherwise snapshot an
            // unauthenticated context for the whole request. Derive identity from the Bearer token and
            // DB-validate the user so the result is correct regardless of middleware ordering. Protected
            // routes are still gated by the JwtBearer handler + [Authorize], so a forged/unsigned token
            // never reaches business code.
            bool itemAuthenticated = context.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] is bool and true;
            MAuthenticateInfoContext mAuth = new(itemAuthenticated);

            mAuth.CorrelationId = context.Request.Headers[CustomHeader.CorrelationId].FirstOrDefault() ?? Guid.NewGuid().ToString();
            mAuth.AccessToken = context.Request.Headers.Authorization;
            string langHeader = context.Request.Headers.AcceptLanguage.ToString();
            mAuth.Language = string.IsNullOrWhiteSpace(langHeader)
                ? "vi-VN"
                : langHeader.Split(',').FirstOrDefault() ?? "vi-VN";

            _resourceSetting[ResourceSettingKeys.Lang] = mAuth.Language;

            if (!string.IsNullOrEmpty(mAuth.AccessToken))
            {
                // InitializeFromToken decodes claims, DB-validates the user (exists + active),
                // and sets IsAuthenticated accordingly — independent of middleware ordering.
                InitializeFromToken(mAuth, mAuth.AccessToken, _dbContext);
            }
            else
            {
                mAuth.ApiKey = _configuration[ClaimConstants.ApiKey];
            }
            authContext = mAuth;
        }
        else if (_amqpContext != null)
        {
            var mAuth = new MAuthenticateInfoContext(true)
            {
                CorrelationId = _amqpContext.GetHeaderByKey(CustomHeader.CorrelationId) ?? Guid.NewGuid().ToString(),
                CurrentUserGuid = _amqpContext.GetHeaderByKey(ClaimConstants.UserIdentifier) ?? string.Empty,
                CurrentUsername = _amqpContext.GetHeaderByKey(ClaimConstants.Username) ?? string.Empty,
                TenantId = _amqpContext.GetHeaderByKey(CustomHeader.TenantId) ?? string.Empty,
                AccessToken = _amqpContext.GetHeaderByKey(ClaimConstants.AccessToken)
            };
            mAuth.IsAuthenticated = !string.IsNullOrEmpty(mAuth.AccessToken);
            authContext = mAuth;
        }
        else
        {
            authContext = new MAuthenticateInfoContext(false);
        }

        return authContext;
    }

    private static void InitializeFromToken(MAuthenticateInfoContext mAuth, string token, MDbContext dbContext)
    {
        try
        {
            var claims = ExtractClaimsFromToken(token);
            mAuth.CurrentUserGuid = GetClaimValue<string>(claims, ClaimConstants.UserIdentifier) ?? string.Empty;
            mAuth.CurrentUsername = GetClaimValue<string>(claims, ClaimConstants.Username) ?? string.Empty;
            mAuth.TenantId = GetClaimValue<string>(claims, ClaimConstants.TenantId) ?? string.Empty;
            mAuth.TokenValidityKey = GetClaimValue<string>(claims, ClaimConstants.TokenValidityKey) ?? string.Empty;
            mAuth.Permission = GetClaimValue<string>(claims, ClaimConstants.Permission) ?? string.Empty;

            if (Guid.TryParse(mAuth.CurrentUserGuid, out Guid userGuid))
            {
                var user = dbContext.Users
                    .Where(u => u.EntityId == userGuid)
                    .Select(u => new
                    {
                        u.EntityId,
                        u.UserName,
                        u.IsActive,
                    })
                    .FirstOrDefault();

                if (user == null || !user.IsActive)
                {
                    // Token references a user that no longer exists or is disabled.
                    mAuth.IsAuthenticated = false;
                    return;
                }

                mAuth.CurrentUserGuid = user.EntityId.ToString();
                mAuth.CurrentUsername = user.UserName;
                // User is valid and active — mark the context authenticated regardless of
                // which middleware resolved it first (order-independent).
                mAuth.IsAuthenticated = true;
            }
            else
            {
                // No usable user identifier in the token.
                mAuth.IsAuthenticated = false;
            }
        }
        catch (Exception ex)
        {
            // No silent catch: surface decode/DB errors so auth failures are diagnosable.
            mAuth.IsAuthenticated = false;
            Console.Error.WriteLine(
                $"[DefaultAuthContextFactory] InitializeFromToken failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static List<Claim> ExtractClaimsFromToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var jwtToken = handler.ReadJwtToken(token.Replace("Bearer ", ""));
            return [.. jwtToken.Claims];
        }
        catch { return []; }
    }

    private static T? GetClaimValue<T>(List<Claim> claims, string claimType)
    {
        var claim = claims.Find(c => c.Type == claimType);
        return claim != null && !string.IsNullOrEmpty(claim.Value)
            ? (T)Convert.ChangeType(claim.Value, typeof(T))
            : default;
    }
}
