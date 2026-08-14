namespace Muonroi.Data.EntityFrameworkCore.Rules.Login;

internal sealed class LoginContext<TPermission, TDbContext>(
    TDbContext dbContext,
    LoginRequestModel request,
    MTokenInfo tokenInfo,
    MAuthenticateTokenHelper<TPermission> tokenHelper,
    IMultiLevelCacheService cacheService,
    string lang)
    where TPermission : Enum
    where TDbContext : MDbContext
{
    public TDbContext DbContext { get; } = dbContext;
    public LoginRequestModel Request { get; } = request;
    public MTokenInfo TokenInfo { get; } = tokenInfo;
    public MAuthenticateTokenHelper<TPermission> TokenHelper { get; } = tokenHelper;
    public IMultiLevelCacheService CacheService { get; } = cacheService;
    public string Lang { get; } = lang;
    public MResponse<LoginResponseModel> Result { get; } = new();
    public MUser? User { get; set; }
    public MUserLoginAttempt? LoginAttempt { get; set; }
}
