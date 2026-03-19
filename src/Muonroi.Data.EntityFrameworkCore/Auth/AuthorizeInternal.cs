using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

// MBB001-exempt: static extension method class — cannot inject IMDateTimeService; DI boundary is at the MDbContext call site.
#pragma warning disable MBB001

namespace Muonroi.Data.EntityFrameworkCore.Auth;

public static class AuthorizeInternal
{
    private const string BearerPrefix = "Bearer ";
    private sealed class AuthorizeInternalLogger { }
    public static async Task<MRefreshToken?> ResolveTokenFromHttpContext<TDbContext>(
        this TDbContext dbContext,
        HttpContext context,
        IMultiLevelCacheService cacheService,
        IMLog<MDbContext>? logger = null,
        IOptions<AuthOptions>? authOptions = null,
        MTokenInfo? tokenInfo = null)
        where TDbContext : MDbContext
    {
        string authorizationHeader = context.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authorizationHeader))
        {
            return null;
        }

        if (!authorizationHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            authorizationHeader = $"{BearerPrefix}{authorizationHeader}";
            context.Request.Headers.Authorization = authorizationHeader;
        }

        return await dbContext.ResolveTokenValidityKey(
            authorizationHeader,
            context,
            cacheService,
            logger,
            authOptions,
            tokenInfo);
    }

    internal static async Task<MRefreshToken?> ResolveTokenValidityKey<TDbContext>(
        this TDbContext dbContext, string authorizationHeader,
        HttpContext context, IMultiLevelCacheService cacheService, IMLog<MDbContext>? logger = null,
        IOptions<AuthOptions>? authOptions = null,
        MTokenInfo? tokenInfo = null)
        where TDbContext : MDbContext
    {
        if (!TryGetValidatedClaims(authorizationHeader, context, tokenInfo, out List<Claim>? claims))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            logger?.Warn("JWT validation failed while resolving token validity context");
            return null;
        }

        AuthClaimMap? claimMap = authOptions?.Value?.ClaimMap;
        string userIdentifierClaimType = claimMap?.UserIdentifier ?? ClaimConstants.UserIdentifier;
        string tokenValidityClaimType = claimMap?.TokenValidityKey ?? ClaimConstants.TokenValidityKey;
        string tenantIdClaimType = claimMap?.TenantId ?? ClaimConstants.TenantId;

        string userIdentifier = GetClaimValue<string>(claims, userIdentifierClaimType) as string ?? string.Empty;
        string tokenValidity = GetClaimValue<string>(claims, tokenValidityClaimType) as string ?? string.Empty;
        string? claimTenantId = GetClaimValue<string>(claims, tenantIdClaimType) as string;

        if (!Guid.TryParse(userIdentifier, out Guid userGuid))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            logger?.Warn("Invalid user identifier in token");
            return null;
        }

        if (tokenInfo?.MultiTenantEnabled == true && !string.IsNullOrWhiteSpace(claimTenantId))
        {
            string? contextTenant = TenantContext.CurrentTenantId;
            if (!string.IsNullOrWhiteSpace(contextTenant) &&
                !string.Equals(contextTenant, claimTenantId, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                logger?.Warn(
                    "Tenant mismatch while resolving token validity. ClaimTenant={ClaimTenant}, ContextTenant={ContextTenant}");
                return null;
            }
        }

        string cacheKey = $"token_validity:{tokenValidity}";
        MRefreshToken? refresh = await cacheService.GetAsync<MRefreshToken>(cacheKey);
        if (refresh is not null && refresh.CreatorUserId != userGuid)
        {
            await cacheService.RemoveAsync(cacheKey);
            refresh = null;
        }

        if (refresh is null)
        {
            refresh = await dbContext.RefreshTokens
                .AsNoTracking()
                .Where(x => x.TokenValidityKey == tokenValidity && x.CreatorUserId == userGuid && !x.IsDeleted)
                .OrderByDescending(x => x.CreationTime)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (refresh is not null)
            {
                await cacheService.SetAsync(cacheKey, refresh, 5);
            }
        }

        if (refresh is null)
        {
            logger?.Warn("Refresh token not found for user {User}", userGuid);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return null;
        }

        if (refresh.IsDeleted || refresh.IsRevoked)
        {
            logger?.Warn("Attempt using revoked token for user {User}", userGuid);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return null;
        }

        context.Items.Add(nameof(IAuthenticateInfoContext.IsAuthenticated), true);
        context.Items[nameof(IAuthenticateInfoContext.TokenValidityKey)] = refresh.TokenValidityKey;
        UserContext.CurrentUserGuid = userIdentifier;
        return refresh;
    }

    private static bool TryGetValidatedClaims(
        string authorizationHeader,
        HttpContext context,
        MTokenInfo? tokenInfo,
        out List<Claim> claims)
    {
        claims = [];

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        if (context.User?.Identity?.IsAuthenticated == true)
        {
            claims = [.. context.User.Claims];
            return claims.Count > 0;
        }

        if (tokenInfo is null)
        {
            return false;
        }

        string token = authorizationHeader;
        if (token.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = token[BearerPrefix.Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        TokenValidationParameters validationParameters = CreateValidationParameters(tokenInfo);
        validationParameters.ValidateLifetime = false;

        try
        {
            ClaimsPrincipal principal = ValidateAndGetPrincipal(token, validationParameters);
            claims = [.. principal.Claims];
            return claims.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static object? GetClaimValue<T>(List<Claim> claims, string claimType)
    {
        Claim? claim = claims.Find(c => c.Type == claimType);
        if (claim == null || string.IsNullOrEmpty(claim.Value))
        {
            return null;
        }

        return Convert.ChangeType(claim.Value, typeof(T));
    }

    public static async Task<MResponse<LoginResponseModel>> ResolveLoginAsync<TDbContext, TPermission>(
        this TDbContext dbContext, LoginRequestModel request,
        MResponse<LoginResponseModel> result,
        MUser existedUser,
        MTokenInfo mTokenInfo,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        IMultiLevelCacheService cacheService,
        string lang,
        List<Claim>? claims,
        CancellationToken cancellationToken)
        where TDbContext : MDbContext
        where TPermission : Enum
    {
        MUserLoginAttempt? loginAttemptHistory = await dbContext.MUserLoginAttempts
            .FirstOrDefaultAsync(x => x.UserGuid == existedUser.EntityId, cancellationToken).ConfigureAwait(false);

        if (IsAccountLocked(loginAttemptHistory, out string? errorMessage))
        {
            result.AddErrorMessage(errorMessage);
            return result;
        }

        if (loginAttemptHistory != null && loginAttemptHistory.LockTo != DateTime.MinValue &&
            loginAttemptHistory.LockTo <= DateTime.UtcNow)
        {
            existedUser.IsActive = true;
            await ResetLoginAttemptOnSuccess(existedUser, loginAttemptHistory, dbContext, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!MPasswordHelper.VerifyPassword(request.Password, existedUser.Password))
        {
            await HandleFailedLoginAttempt(existedUser, loginAttemptHistory, dbContext, cancellationToken)
                .ConfigureAwait(false);
            result.AddError(nameof(SystemEnum.InvalidCredentials), lang);
            return result;
        }


        List<TPermission> permissions = await GetPermissionsOfUser<TDbContext, TPermission>(existedUser!.Id, dbContext, cacheService)
            .ConfigureAwait(false);

        GenerateAccessToken(existedUser, permissions, out string? accessToken, out string? tokenValidate, tokenHelper, claims);

        GenerateRefreshToken(out string? refreshToken);

        result.Result = await GenerateLoginReply(accessToken, refreshToken, existedUser, tokenValidate, mTokenInfo,
            dbContext, cacheService, permissions).ConfigureAwait(false);

        await ResetLoginAttemptOnSuccess(existedUser, loginAttemptHistory, dbContext, cancellationToken)
            .ConfigureAwait(false);

        return result;
    }

    internal static async Task<MResponse<string>> ResolveTokenValidity<TDbContext>(this TDbContext dbContext,
        string tokenValidity, string lang, CancellationToken cancellationToken)
        where TDbContext : MDbContext
    {
        MResponse<string> result = new();
        if (string.IsNullOrEmpty(tokenValidity))
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), lang);
            return result;
        }

        try
        {
            MRefreshToken? refresh = await dbContext.RefreshTokens
                .AsNoTracking().FirstOrDefaultAsync(x =>
                    x.TokenValidityKey == tokenValidity, cancellationToken);
            if (refresh is null || refresh.IsDeleted || refresh.IsRevoked)
            {
                result.AddError(nameof(SystemEnum.InvalidCredentials), lang);
                return result;
            }

            result.Result = refresh.Token;
        }
        catch (Exception)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), lang);
        }

        return result;
    }

    public static async Task<MResponse<RefreshTokenResponseModel>> ResolveRefreshToken<TDbContext, TPermission>(
        this TDbContext dbContext,
        RefreshTokenRequestModel request,
        MResponse<RefreshTokenResponseModel> result,
        MTokenInfo mTokenInfo,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        MUser existedUser,
        IMultiLevelCacheService cacheService,
        string lang,
        List<Claim>? claims,
        CancellationToken cancellationToken,
        IOptions<AuthOptions>? authOptions = null)
        where TDbContext : MDbContext
        where TPermission : Enum
    {
        string token = request.AccessToken.Replace("Bearer ", "");

        if (!await VerifyPrincipalFromExpiredToken(token, mTokenInfo).ConfigureAwait(false))
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), lang);
            return result;
        }

        TokenValidationParameters validationParameters = CreateValidationParameters(mTokenInfo);

        ClaimsPrincipal principal;
        try
        {
            principal = ValidateAndGetPrincipal(token, validationParameters);
        }
        catch (Exception)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), lang);
            return result;
        }

        List<Claim> claimsToken = [.. principal.Claims];
        claimsToken.AddRange(claims ?? []);
        AuthClaimMap? claimMap = authOptions?.Value?.ClaimMap;
        string userIdentifierClaimType = claimMap?.UserIdentifier ?? ClaimConstants.UserIdentifier;
        string tokenValidityClaimType = claimMap?.TokenValidityKey ?? ClaimConstants.TokenValidityKey;
        string usernameClaimType = claimMap?.Username ?? ClaimConstants.Username;
        string permissionClaimType = claimMap?.Permission ?? ClaimConstants.Permission;

        string userIdentifier = claimsToken.Find(c => c.Type == userIdentifierClaimType)?.Value ?? string.Empty;
        string tokenValidity = claimsToken.Find(c => c.Type == tokenValidityClaimType)?.Value ?? string.Empty;

        MRefreshToken? refresh = await dbContext.RefreshTokens.SingleOrDefaultAsync(x =>
            x.Token == request.RefreshToken &&
            x.TokenValidityKey == tokenValidity &&
            x.CreatorUserId == Guid.Parse(userIdentifier), cancellationToken).ConfigureAwait(false);

        if (refresh is null || refresh.IsDeleted || refresh.IsRevoked || refresh.ExpiredDate <= DateTime.UtcNow)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), lang);
            return result;
        }

        if (refresh.CreationTime.AddMinutes(mTokenInfo.RefreshTokenTtl) <= DateTime.UtcNow)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), lang);
            return result;
        }

        if (refresh.LastUsedDate.AddMinutes(mTokenInfo.RefreshTokenEim) <= DateTime.UtcNow)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), lang);
            return result;
        }

        Claim? tokenKey = claimsToken.Find(c => c.Type == tokenValidityClaimType);


        if (tokenKey is not null)
        {
            Claim userId = claimsToken.Find(c => c.Type == userIdentifierClaimType)!;
            Claim userName = claimsToken.Find(c => c.Type == usernameClaimType)!;
            Claim permission = claimsToken.Find(c => c.Type == permissionClaimType)!;
            Claim aud = claimsToken.Find(c => c.Type == "aud")!;
            _ = claimsToken.Remove(userId);
            _ = claimsToken.Remove(aud);
            _ = claimsToken.Remove(permission);
            _ = claimsToken.Remove(userName);
            _ = claimsToken.Remove(tokenKey);
        }

        List<TPermission> permissions = await GetPermissionsOfUser<TDbContext, TPermission>(existedUser!.Id, dbContext, cacheService)
            .ConfigureAwait(false);

        GenerateAccessToken(existedUser, permissions, out string? newAccessToken, out string? tokenValidate, tokenHelper,
            claimsToken);

        GenerateRefreshToken(out string? newRefreshToken);

        await SaveRefreshToken(newRefreshToken, dbContext, Guid.Parse(userIdentifier), tokenValidate, mTokenInfo,
            cacheService).ConfigureAwait(false);

        await RevokeRefreshToken(refresh, Guid.Parse(userIdentifier), dbContext, cacheService, "RefreshToken")
            .ConfigureAwait(false);

        result.Result = new RefreshTokenResponseModel
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken
        };

        return result;
    }

    private static TokenValidationParameters CreateValidationParameters(MTokenInfo mTokenInfo)
    {
        SecurityKey signingKey;
        if (mTokenInfo.UseRsa)
        {
            RSA rsa = RSA.Create();
            string publicKey = mTokenInfo.GetEffectivePublicKey();
            rsa.ImportFromPem(publicKey.ToCharArray());
            signingKey = new RsaSecurityKey(rsa);
        }
        else
        {
            signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(mTokenInfo.SymmetricSecretKey));
        }

        IEnumerable<SecurityKey> ResolveSigningKeys(string? kid)
        {
            if (mTokenInfo.UseRsa || string.IsNullOrWhiteSpace(kid))
            {
                return [signingKey];
            }

            if (mTokenInfo.SigningKeysByTenant.TryGetValue(kid, out string? tenantKey) &&
                !string.IsNullOrWhiteSpace(tenantKey))
            {
                SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(tenantKey))
                {
                    KeyId = kid
                };
                return [key, signingKey];
            }

            return [signingKey];
        }

        TokenValidationParameters parameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            IssuerSigningKeyResolver = (_, _, kid, _) => ResolveSigningKeys(kid),
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = mTokenInfo.Issuer,
            ValidAudience = mTokenInfo.Audience
        };
        return parameters;
    }


    private static ClaimsPrincipal ValidateAndGetPrincipal(string accessToken,
        TokenValidationParameters validationParameters)
    {
        JwtSecurityTokenHandler tokenHandler = new();
        return tokenHandler.ValidateToken(accessToken, validationParameters, out _);
    }


    public static async Task HandleFailedLoginAttempt<TDbContext>(MUser existedUser,
        MUserLoginAttempt? loginAttemptHistory, TDbContext dbContext,
        CancellationToken cancellationToken)
        where TDbContext : MDbContext
    {
        MUserLoginAttempt loginAttempt = loginAttemptHistory ?? new MUserLoginAttempt
        {
            UserGuid = existedUser.EntityId,
            CreationTime = DateTime.UtcNow,
            AttemptTime = 0
        };

        if (loginAttempt.LockTo != DateTime.MinValue && loginAttempt.LockTo <= DateTime.UtcNow)
        {
            loginAttempt.AttemptTime = 0;
            loginAttempt.LockTo = DateTime.MinValue;
        }

        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        loginAttempt.AttemptTime++;

        UpdateLoginAttemptStatus(existedUser, loginAttempt);

        _ = loginAttemptHistory == null
            ? await dbContext.MUserLoginAttempts.AddAsync(loginAttempt, cancellationToken).ConfigureAwait(false)
            : dbContext.MUserLoginAttempts.Update(loginAttempt);

        _ = dbContext.Users.Update(existedUser);
        _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void UpdateLoginAttemptStatus(MUser existedUser, MUserLoginAttempt loginAttempt)
    {
        switch (loginAttempt.AttemptTime)
        {
            case 3:
                loginAttempt.LockTo = DateTime.UtcNow.AddMinutes(5);
                break;
            case 4:
                loginAttempt.LockTo = DateTime.UtcNow.AddMinutes(10);
                break;
            case 5:
                loginAttempt.LockTo = DateTime.UtcNow.AddMinutes(30);
                break;
            case 6:
                loginAttempt.LockTo = DateTime.MaxValue;
                break;
        }

        if (loginAttempt.AttemptTime >= 3)
        {
            existedUser.IsActive = false;
        }
    }

    private static async Task<bool> VerifyPrincipalFromExpiredToken(string token, MTokenInfo mTokenInfo)
    {
        TokenValidationParameters validationParameters = CreateValidationParameters(mTokenInfo);
        JwtSecurityTokenHandler tokenHandler = new();
        TokenValidationResult result = await tokenHandler.ValidateTokenAsync(token, validationParameters).ConfigureAwait(false);
        return result.IsValid;
    }

    private static async Task RevokeRefreshToken<TDbContext>(MRefreshToken token, Guid userId, TDbContext dbContext,
        IMultiLevelCacheService cacheService, string reason = "")
        where TDbContext : MDbContext
    {
        token.RevokedDate = DateTime.UtcNow;
        token.ReasonRevoked = reason;
        token.IsRevoked = true;
        token.LastModificationTime = DateTime.UtcNow;
        token.LastModificationUserId = userId;
        _ = dbContext.Update(token);
        _ = await dbContext.SaveChangesAsync().ConfigureAwait(false);
        string cacheKey = $"token_validity:{token.TokenValidityKey}";
        await cacheService.RemoveAsync(cacheKey);
    }

    internal static async Task<LoginResponseModel> GenerateLoginReply<TDbContext, TPermission>(string accessToken,
        string refreshToken,
        MUser existedUser,
        string tokenValidate,
        MTokenInfo mTokenInfo,
        TDbContext dbContext,
        IMultiLevelCacheService cacheService,
        List<TPermission> permissions
    )
        where TDbContext : MDbContext
        where TPermission : Enum
    {
        LoginResponseModel loginReply = new()
        {
            Username = existedUser.UserName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Surname = existedUser.Surname,
            Name = existedUser.Name,
            EmailAddress = existedUser.EmailAddress,
            PhoneNumber = existedUser.PhoneNumber,
            IsPhoneNumberConfirmed = existedUser.IsPhoneNumberConfirmed,
            IsEmailConfirmed = existedUser.IsEmailConfirmed,
            IsActive = existedUser.IsActive,
            IsUseThirdPartyLogin = existedUser.IsUseThirdPartyLogin,
            ExternalLoginToken = existedUser.ExternalLoginToken,
            ExternalLoginProvider = existedUser.ExternalLoginProvider,
            Permissions = [.. permissions.Select(p => p!.ToString())]
        };

        await SaveRefreshToken(loginReply.RefreshToken, dbContext, existedUser.EntityId, tokenValidate, mTokenInfo,
            cacheService).ConfigureAwait(false);

        return loginReply;
    }

    internal static bool IsAccountLocked(MUserLoginAttempt? loginAttemptHistory, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (loginAttemptHistory != null && loginAttemptHistory.LockTo > DateTime.UtcNow)
        {
            TimeSpan remainingLockTime = loginAttemptHistory.LockTo - DateTime.UtcNow;
            errorMessage = remainingLockTime.ToString();
            return true;
        }

        return false;
    }

    internal static async Task ResetLoginAttemptOnSuccess<TDbContext>(MUser existedUser,
        MUserLoginAttempt? loginAttemptHistory,
        TDbContext dbContext, CancellationToken cancellationToken)
        where TDbContext : MDbContext
    {
        if (loginAttemptHistory != null)
        {
            loginAttemptHistory.AttemptTime = 0;
            loginAttemptHistory.LockTo = DateTime.MinValue;
            _ = dbContext.Update(loginAttemptHistory);
        }

        if (!existedUser.IsActive)
        {
            existedUser.IsActive = true;
            _ = dbContext.Users.Update(existedUser);
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<List<TPermission>> GetPermissionsOfUser<TDbContext, TPermission>(long userId,
        TDbContext dbContext,
        IMultiLevelCacheService? cacheService = null,
        int cacheMinutes = 5)
        where TDbContext : MDbContext
        where TPermission : Enum
    {
        string cacheKey = RbacCacheKeys.UserPermissionsByNumericId(userId);

        if (cacheService is not null)
        {
            List<string>? cachedPermissions = await cacheService.GetAsync<List<string>>(cacheKey);
            if (cachedPermissions is null)
            {
                cachedPermissions = await cacheService.GetAsync<List<string>>(RbacCacheKeys.LegacyUserPermissions(userId));
                if (cachedPermissions is not null)
                {
                    await cacheService.SetAsync(cacheKey, cachedPermissions, cacheMinutes);
                }
            }

            if (cachedPermissions is not null)
            {
                return [.. cachedPermissions.Where(name => Enum.TryParse(typeof(TPermission), name, out _)).Select(name => (TPermission)Enum.Parse(typeof(TPermission), name))];
            }
        }

        List<string> permissionNames = await (from user in dbContext.Set<MUser>().AsNoTracking()
                                              join userRole in dbContext.Set<MUserRole>().AsNoTracking() on user.EntityId equals userRole.UserId
                                              join role in dbContext.Set<MRole>().AsNoTracking() on userRole.RoleId equals role.EntityId
                                              join rolePermission in dbContext.Set<MRolePermission>().AsNoTracking() on role.EntityId equals
                                                  rolePermission.RoleId
                                              join permission in dbContext.Set<MPermission>().AsNoTracking() on rolePermission.PermissionId equals
                                                  permission.EntityId
                                              where user.Id == userId
                                                    && !user.IsDeleted
                                                    && !role.IsDeleted
                                                    && !permission.IsDeleted
                                                    && !rolePermission.IsDeleted
                                              select permission.Name).Distinct().ToListAsync().ConfigureAwait(false);

        if (cacheService is not null && permissionNames.Count > 0)
        {
            await cacheService.SetAsync(cacheKey, permissionNames, cacheMinutes);
            await cacheService.RemoveAsync(RbacCacheKeys.LegacyUserPermissions(userId));
        }

        return [.. permissionNames.Where(name => Enum.TryParse(typeof(TPermission), name, out _)).Select(name => (TPermission)Enum.Parse(typeof(TPermission), name))];
    }


    internal static void GenerateAccessToken<TPermission>(MUser user,
        List<TPermission> permissions,
        out string accessToken,
        out string tokenValidityKey,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        List<Claim>? claims = null)
        where TPermission : Enum
    {
        tokenValidityKey = Guid.NewGuid().ToString();

        MUserModel userModel = new(user.EntityId.ToString(), user.UserName, tokenValidityKey, user.Name, user.Surname,
            user.PhoneNumber, user.EmailAddress, TenantContext.CurrentTenantId);

        List<Claim> allClaims = claims is not null ? [.. claims] : [];

        long permissionsBitmask = MPermissionExtension<TPermission>.CalculatePermissionsBitmask(permissions);
        allClaims.Add(new Claim(ClaimConstants.TokenValidityKey, tokenValidityKey));
        allClaims.Add(new Claim(ClaimConstants.Permission, permissionsBitmask.ToString()));

        accessToken = tokenHelper.GenerateAuthenticateToken(userModel, permissions, allClaims);
    }

    internal static void GenerateRefreshToken(out string refreshToken)
    {
        byte[] randomNumber = new byte[32];
        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        refreshToken = Convert.ToBase64String(randomNumber);
    }

    private static async Task SaveRefreshToken<TDbContext>(string refreshToken,
        TDbContext dbContext, Guid userId, string tokenValidityKey, MTokenInfo mTokenInfo,
        IMultiLevelCacheService cacheService)
        where TDbContext : MDbContext
    {
        MRefreshToken token = new()
        {
            Token = refreshToken,
            TokenValidityKey = tokenValidityKey,
            ExpiredDate = DateTime.UtcNow.AddMinutes(mTokenInfo.RefreshTokenTtl),
            IsDeleted = false,
            IsRevoked = false,
            LastUsedDate = DateTime.UtcNow,
            CreatorUserId = userId,
            CreationTime = DateTime.UtcNow
        };
        _ = await dbContext.RefreshTokens.AddAsync(token).ConfigureAwait(false);
        _ = await dbContext.SaveChangesAsync().ConfigureAwait(false);
        string cacheKey = $"token_validity:{tokenValidityKey}";
        await cacheService.SetAsync(cacheKey, token, mTokenInfo.RefreshTokenTtl);
    }
}
