using System.IdentityModel.Tokens.Jwt;

namespace Muonroi.AspNetCore.Controllers;

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

    public IAuthenticateInfoContext Create()
    {
        IAuthenticateInfoContext authContext;

        if (_httpContextAccessor.HttpContext != null)
        {
            MAuthenticateInfoContext mAuth = new(_httpContextAccessor.HttpContext.Items[nameof(MAuthenticateInfoContext.IsAuthenticated)] is bool and true);
            HttpContext? context = _httpContextAccessor.HttpContext;

            mAuth.CorrelationId = context.Request.Headers[CustomHeader.CorrelationId].FirstOrDefault() ?? Guid.NewGuid().ToString();
            mAuth.AccessToken = context.Request.Headers.Authorization;
            string langHeader = context.Request.Headers.AcceptLanguage.ToString();
            mAuth.Language = string.IsNullOrWhiteSpace(langHeader)
                ? "vi-VN"
                : langHeader.Split(',').FirstOrDefault() ?? "vi-VN";

            _resourceSetting[ResourceSettingKeys.Lang] = mAuth.Language;

            if (mAuth.IsAuthenticated && !string.IsNullOrEmpty(mAuth.AccessToken))
            {
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
                    mAuth.IsAuthenticated = false;
                    return;
                }

                mAuth.CurrentUserGuid = user.EntityId.ToString();
                mAuth.CurrentUsername = user.UserName;

            }
        }
        catch { /* Ignore token parsing errors */ }
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
