namespace Muonroi.Data.EntityFrameworkCore.Rules.Login;

internal sealed class ValidateLoginInfoRule<TPermission, TDbContext> : IRule<LoginContext<TPermission, TDbContext>>
    where TPermission : Enum
    where TDbContext : MDbContext
{
    public string Name => Code;
    public string Code => "ValidateLoginInfo";
    public int Order => 1;
    public IReadOnlyList<string> DependsOn => [];
    public IEnumerable<Type> Dependencies => [];
    public HookPoint HookPoint => HookPoint.BeforePersist;
    public RuleType Type => RuleType.Validation;

    public Task<RuleResult> EvaluateAsync(LoginContext<TPermission, TDbContext> context, FactBag facts, CancellationToken ct)
    {
        return Task.FromResult(RuleResult.Passed());
    }

    public Task ExecuteAsync(LoginContext<TPermission, TDbContext> context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Request.Username) || string.IsNullOrEmpty(context.Request.Password))
        {
            context.Result.AddError(nameof(SystemEnum.InvalidLoginInfo), context.Lang);
        }

        return Task.CompletedTask;
    }
}
