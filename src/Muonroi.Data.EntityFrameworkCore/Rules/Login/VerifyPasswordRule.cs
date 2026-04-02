namespace Muonroi.Data.EntityFrameworkCore.Rules.Login;

internal sealed class VerifyPasswordRule<TPermission, TDbContext>(IPasswordHasher passwordHasher) : IRule<LoginContext<TPermission, TDbContext>>
    where TPermission : Enum
    where TDbContext : MDbContext
{
    public string Name => Code;
    public string Code => "VerifyPassword";
    public int Order => 4;
    public IReadOnlyList<string> DependsOn => ["CheckAccountLock"];
    public IEnumerable<Type> Dependencies => [];
    public HookPoint HookPoint => HookPoint.BeforePersist;
    public RuleType Type => RuleType.Validation;

    public Task<RuleResult> EvaluateAsync(LoginContext<TPermission, TDbContext> context, FactBag facts, CancellationToken ct)
    {
        return Task.FromResult(RuleResult.Passed());
    }

    public async Task ExecuteAsync(LoginContext<TPermission, TDbContext> context, CancellationToken cancellationToken = default)
    {
        if (!context.Result.IsOk || context.User is null)
        {
            return;
        }

        if (!passwordHasher.VerifyPassword(context.Request.Password, context.User.Password))
        {
            await AuthorizeInternal.HandleFailedLoginAttempt(context.User, context.LoginAttempt, context.DbContext, cancellationToken).ConfigureAwait(false);
            context.Result.AddError(nameof(SystemEnum.InvalidCredentials), context.Lang);
        }
    }
}
