using Muonroi.Core.Helpers;

namespace Muonroi.AspNetCore.Services;

public class AuthService<TPermission, TDbContext>(
    TDbContext dbContext,
    IAuthenticateInfoContext context,
    IAuthenticateRepository? authenticateRepository,
    IMDateTimeService dateTimeService) : IAuthService<TPermission, TDbContext>
    where TPermission : Enum
    where TDbContext : MDbContext
{
    public async Task<MResponse<object>> LogoutAsync(CancellationToken cancellationToken)
    {
        MResponse<object> result = new();

        if (!Guid.TryParse(context.CurrentUserGuid, out Guid userGuid))
        {
            result.AddError(nameof(SystemEnum.UserNotFound), context.Language);
            return result;
        }

        int rowsAffected;
        try
        {
            rowsAffected = await dbContext.Set<MRefreshToken>()
                .Where(rt => rt.TokenValidityKey == context.TokenValidityKey
                             && rt.CreatorUserId == userGuid
                             && !rt.IsDeleted
                             && !rt.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedDate, dateTimeService.UtcNow())
                    .SetProperty(rt => rt.ReasonRevoked, "Logout"), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            List<MRefreshToken> tokens = await dbContext.Set<MRefreshToken>()
                .Where(rt => rt.TokenValidityKey == context.TokenValidityKey
                             && rt.CreatorUserId == userGuid
                             && !rt.IsDeleted
                             && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (MRefreshToken? token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedDate = dateTimeService.UtcNow();
                token.ReasonRevoked = "Logout";
                _ = dbContext.Update(token);
            }
            rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (rowsAffected == 0)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), context.Language);
        }

        return result;
    }

    public async Task<MResponse<object>> LogoutAllAsync(CancellationToken cancellationToken)
    {
        MResponse<object> result = new();

        if (!Guid.TryParse(context.CurrentUserGuid, out Guid userGuid))
        {
            result.AddError(nameof(SystemEnum.UserNotFound), context.Language);
            return result;
        }

        try
        {
            _ = await dbContext.Set<MRefreshToken>()
                .Where(rt => rt.CreatorUserId == userGuid && !rt.IsDeleted && !rt.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedDate, dateTimeService.UtcNow())
                    .SetProperty(rt => rt.ReasonRevoked, "LogoutAll"), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            List<MRefreshToken> tokens = await dbContext.Set<MRefreshToken>()
                .Where(rt => rt.CreatorUserId == userGuid && !rt.IsDeleted && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (MRefreshToken? token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedDate = dateTimeService.UtcNow();
                token.ReasonRevoked = "LogoutAll";
                _ = dbContext.Update(token);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return result;
    }


    public async Task<MResponse<LoginResponseModel>> RegisterAsync(RegisterRequestModel request,
        CancellationToken cancellationToken)
    {
        MResponse<LoginResponseModel> result = new();

        MUser? existingUser = await dbContext.Set<MUser>()
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserName == request.UserName, cancellationToken);
        if (existingUser != null)
        {
            result.AddError(nameof(SystemEnum.UserAlreadyExists), context.Language);
            return result;
        }

        MUser user = new()
        {
            UserName = request.UserName,
            EmailAddress = request.Email,
            Password = MPasswordHelper.HashPassword(request.Password, out string? salt),
            Salt = salt,
            Name = request.Name,
            Surname = request.Surname,
            PhoneNumber = request.PhoneNumber,
            IsActive = request.IsActive,
            IsTwoFactorEnabled = request.IsTwoFactorEnabled,
            IsUseThirdPartyLogin = request.IsUseThirdPartyLogin,
            ExternalLoginToken = request.ExternalLoginToken,
            ExternalLoginProvider = request.ExternalLoginProvider
        };
        _ = await dbContext.Set<MUser>().AddAsync(user, cancellationToken);
        _ = await dbContext.SaveChangesAsync(cancellationToken);

        if (authenticateRepository is null)
        {
            return new MResponse<LoginResponseModel>();
        }

        MResponse<LoginResponseModel> loginResult = await authenticateRepository.Login(new LoginRequestModel
        {
            Username = request.UserName,
            Password = request.Password
        }, cancellationToken);

        if (loginResult.Result is null || !loginResult.IsOk)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), context.Language);
            return result;
        }

        result.Result = loginResult.Result;
        return result;
    }

    public async Task<MResponse<LoginResponseModel>> LoginAsync(LoginRequestModel request,
        MTokenInfo tokenInfo,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        IMultiLevelCacheService cacheService,
        CancellationToken cancellationToken)
    {
        if (authenticateRepository is null)
        {
            return new MResponse<LoginResponseModel>();
        }

        return await authenticateRepository.Login(request, cancellationToken);
    }

    public async Task<MResponse<RefreshTokenResponseModel>> RefreshTokenAsync(RefreshTokenRequestModel request,
        MTokenInfo tokenInfo,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        IMultiLevelCacheService cacheService,
        CancellationToken cancellationToken)
    {
        MResponse<RefreshTokenResponseModel> result = new();
        MUser? existedUser = await dbContext.Set<MUser>()
            .FirstOrDefaultAsync(x => x.EntityId == Guid.Parse(context.CurrentUserGuid), cancellationToken);

        if (existedUser is null)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), context.Language);
            return result;
        }

        return await dbContext.ResolveRefreshToken(request,
            result,
            tokenInfo,
            tokenHelper,
            existedUser,
            cacheService,
            context.Language,
            claims: null,
            cancellationToken);
    }
}
