namespace Muonroi.Data.EntityFrameworkCore.Rules.Login;

internal sealed class GenerateTokenRule<TPermission, TDbContext> : IRule<LoginContext<TPermission, TDbContext>>
    where TPermission : Enum
    where TDbContext : MDbContext
{
    public string Name => Code;
    public string Code => "GenerateToken";
    public int Order => 5;
    public IReadOnlyList<string> DependsOn => ["VerifyPassword"];
    public IEnumerable<Type> Dependencies => [];
    public HookPoint HookPoint => HookPoint.BeforePersist;
    public RuleType Type => RuleType.Business;

    public Task<RuleResult> EvaluateAsync(LoginContext<TPermission, TDbContext> context, FactBag facts,
        CancellationToken ct)
    {
        return Task.FromResult(RuleResult.Passed());
    }

    public async Task ExecuteAsync(LoginContext<TPermission, TDbContext> context,
        CancellationToken cancellationToken = default)
    {
        if (!context.Result.IsOk || context.User is null)
        {
            return;
        }

        List<TPermission> permissions = await AuthorizeInternal
            .GetPermissionsOfUser<TDbContext, TPermission>(context.User.Id, context.DbContext, context.CacheService)
            .ConfigureAwait(false);

        // Claims are generated from database permissions, not from client request
        AuthorizeInternal.GenerateAccessToken(context.User, permissions, out string? accessToken, out string? tokenValidate,
            context.TokenHelper, claims: null);

        AuthorizeInternal.GenerateRefreshToken(out string? refreshToken);

        context.Result.Result = await AuthorizeInternal.GenerateLoginReply(accessToken, refreshToken, context.User,
                tokenValidate, context.TokenInfo, context.DbContext, context.CacheService, permissions)
            .ConfigureAwait(false);

        await AuthorizeInternal
            .ResetLoginAttemptOnSuccess(context.User, context.LoginAttempt, context.DbContext, cancellationToken)
            .ConfigureAwait(false);
    }
}
