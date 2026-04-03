namespace Muonroi.Data.EntityFrameworkCore.Rules.Login;

internal sealed class LoadUserRule<TPermission, TDbContext> : IRule<LoginContext<TPermission, TDbContext>>
    where TPermission : Enum
    where TDbContext : MDbContext
{
    public string Name => Code;
    public string Code => "LoadUser";
    public int Order => 2;
    public IReadOnlyList<string> DependsOn => ["ValidateLoginInfo"];
    public IEnumerable<Type> Dependencies => [];
    public HookPoint HookPoint => HookPoint.BeforePersist;
    public RuleType Type => RuleType.Validation;

    public Task<RuleResult> EvaluateAsync(LoginContext<TPermission, TDbContext> context, FactBag facts, CancellationToken ct)
    {
        return Task.FromResult(RuleResult.Passed());
    }

    public async Task ExecuteAsync(LoginContext<TPermission, TDbContext> context, CancellationToken cancellationToken = default)
    {
        if (!context.Result.IsOk)
        {
            return;
        }

        context.User = await context.DbContext.Set<MUser>()
            .FirstOrDefaultAsync(x => x.UserName == context.Request.Username, cancellationToken)
            .ConfigureAwait(false);

        if (context.User is null)
        {
            context.Result.AddError(nameof(SystemEnum.InvalidCredentials), context.Lang);
        }
    }
}
