namespace Muonroi.Data.EntityFrameworkCore.Rules.Login;

internal sealed class CheckAccountLockRule<TPermission, TDbContext>(IMDateTimeService dateTimeService) : IRule<LoginContext<TPermission, TDbContext>>
    where TPermission : Enum
    where TDbContext : MDbContext
{
    public string Name => Code;
    public string Code => "CheckAccountLock";
    public int Order => 3;
    public IReadOnlyList<string> DependsOn => ["LoadUser"];
    public IEnumerable<Type> Dependencies => [];
    public HookPoint HookPoint => HookPoint.BeforePersist;
    public RuleType Type => RuleType.Validation;

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

        context.LoginAttempt = await context.DbContext.MUserLoginAttempts
            .FirstOrDefaultAsync(x => x.UserGuid == context.User.EntityId, cancellationToken)
            .ConfigureAwait(false);

        // NOTE: AuthorizeInternal.IsAccountLocked should be migrated or logic inline
        // For now, assume it's available or logic will be added
        if (context.LoginAttempt != null && context.LoginAttempt.LockTo > dateTimeService.UtcNow())
        {
            context.Result.AddErrorMessage("Account is locked");
            return;
        }

        if (context.LoginAttempt != null &&
            context.LoginAttempt.LockTo != DateTime.MinValue &&
            context.LoginAttempt.LockTo <= dateTimeService.UtcNow())
        {
            context.User.IsActive = true;
        }
    }
}
