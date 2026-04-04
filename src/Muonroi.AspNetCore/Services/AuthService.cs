namespace Muonroi.AspNetCore.Services;

/// <summary>
/// Service for user authentication and session management.
/// </summary>
/// <typeparam name="TPermission">The type of the permission enum.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public class AuthService<TPermission, TDbContext>(
    TDbContext dbContext,
    IAuthenticateInfoContext context,
    IAuthenticateRepository? authenticateRepository,
    IMDateTimeService dateTimeService,
    IPasswordHasher passwordHasher) : IAuthService<TPermission, TDbContext>
    where TPermission : Enum
    where TDbContext : MDbContext
{
    private readonly TDbContext _dbContext = dbContext;
    private readonly IAuthenticateInfoContext _context = context;
    private readonly IAuthenticateRepository? _authenticateRepository = authenticateRepository;
    private readonly IMDateTimeService _dateTimeService = dateTimeService;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;

    /// <summary>
    /// Logs out the current user by revoking their active refresh token asynchronously.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<MResponse<object>> LogoutAsync(CancellationToken cancellationToken)
    {
        MResponse<object> result = new();

        if (!Guid.TryParse(_context.CurrentUserGuid, out Guid userGuid))
        {
            result.AddError(nameof(SystemEnum.UserNotFound), _context.Language);
            return result;
        }

        try
        {
            var rowsAffected = await _dbContext.Set<MRefreshToken>()
                .Where(rt => rt.TokenValidityKey == _context.TokenValidityKey
                             && rt.CreatorUserId == userGuid
                             && !rt.IsDeleted
                             && !rt.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedDate, _dateTimeService.UtcNow())
                    .SetProperty(rt => rt.ReasonRevoked, "Logout"), cancellationToken);

            if (rowsAffected > 0)
            {
                return result;
            }
        }
        catch (Exception)
        {
            // Fallback for providers that don't support ExecuteUpdate (like InMemory)
        }

        List<MRefreshToken> tokens = await _dbContext.Set<MRefreshToken>()
            .Where(rt => rt.TokenValidityKey == _context.TokenValidityKey
                         && rt.CreatorUserId == userGuid
                         && !rt.IsDeleted
                         && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        if (tokens.Count == 0)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), _context.Language);
            return result;
        }

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedDate = _dateTimeService.UtcNow();
            token.ReasonRevoked = "Logout";
        }
        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    /// <inheritdoc/>
    public async Task<MResponse<object>> LogoutAllAsync(CancellationToken cancellationToken)
    {
        MResponse<object> result = new();

        if (!Guid.TryParse(_context.CurrentUserGuid, out Guid userGuid))
        {
            result.AddError(nameof(SystemEnum.UserNotFound), _context.Language);
            return result;
        }

        try
        {
            await _dbContext.Set<MRefreshToken>()
                .Where(rt => rt.CreatorUserId == userGuid && !rt.IsDeleted && !rt.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedDate, _dateTimeService.UtcNow())
                    .SetProperty(rt => rt.ReasonRevoked, "LogoutAll"), cancellationToken);
        }
        catch (Exception)
        {
            List<MRefreshToken> tokens = await _dbContext.Set<MRefreshToken>()
                .Where(rt => rt.CreatorUserId == userGuid && !rt.IsDeleted && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedDate = _dateTimeService.UtcNow();
                token.ReasonRevoked = "LogoutAll";
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<MResponse<LoginResponseModel>> RegisterAsync(RegisterRequestModel request,
        CancellationToken cancellationToken)
    {
        MResponse<LoginResponseModel> result = new();

        MUser? existingUser = await _dbContext.Set<MUser>()
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserName == request.UserName, cancellationToken);
        if (existingUser != null)
        {
            result.AddError(nameof(SystemEnum.UserAlreadyExists), _context.Language);
            return result;
        }

        MUser user = new()
        {
            UserName = request.UserName,
            EmailAddress = request.Email,
            Password = _passwordHasher.HashPassword(request.Password, out string? salt),
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
        _ = await _dbContext.Set<MUser>().AddAsync(user, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (_authenticateRepository is null)
        {
            result.AddError(nameof(SystemEnum.UnhandledException), _context.Language);
            return result;
        }

        MResponse<LoginResponseModel> loginResult = await _authenticateRepository.Login(new LoginRequestModel
        {
            Username = request.UserName,
            Password = request.Password
        }, cancellationToken);

        if (loginResult?.Result is null || !loginResult.IsOk)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), _context.Language);
            return result;
        }

        result.Result = loginResult.Result;
        return result;
    }

    /// <inheritdoc/>
    public async Task<MResponse<LoginResponseModel>> LoginAsync(LoginRequestModel request,
        MTokenInfo tokenInfo,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        IMultiLevelCacheService cacheService,
        CancellationToken cancellationToken)
    {
        if (_authenticateRepository is null)
        {
            var res = new MResponse<LoginResponseModel>();
            res.AddError(nameof(SystemEnum.UnhandledException), _context.Language);
            return res;
        }

        return await _authenticateRepository.Login(request, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MResponse<RefreshTokenResponseModel>> RefreshTokenAsync(RefreshTokenRequestModel request,
        MTokenInfo tokenInfo,
        MAuthenticateTokenHelper<TPermission> tokenHelper,
        IMultiLevelCacheService cacheService,
        CancellationToken cancellationToken)
    {
        MResponse<RefreshTokenResponseModel> result = new();
        MUser? existedUser = await _dbContext.Set<MUser>()
            .FirstOrDefaultAsync(x => x.EntityId == Guid.Parse(_context.CurrentUserGuid), cancellationToken);

        if (existedUser is null)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), _context.Language);
            return result;
        }

        return await _dbContext.ResolveRefreshToken(request,
            result,
            tokenInfo,
            tokenHelper,
            existedUser,
            cacheService,
            _context.Language,
            claims: null,
            cancellationToken);
    }
}
