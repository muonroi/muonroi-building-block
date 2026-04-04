using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class RuleEngineTransactionTests
{
    private sealed class InsertUserRule : IRule<MDbContext>
    {
        public RuleType Type => RuleType.Business;

        public string Code => throw new NotImplementedException();

        public int Order => throw new NotImplementedException();

        public IReadOnlyList<string> DependsOn => throw new NotImplementedException();

        public HookPoint HookPoint => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public IEnumerable<Type> Dependencies => throw new NotImplementedException();

        public Task<RuleResult> EvaluateAsync(MDbContext ctx, FactBag facts, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public async Task ExecuteAsync(MDbContext context, CancellationToken cancellationToken = default)
        {
            MUser user = new()
            {
                UserName = "u",
                EmailAddress = "a@b.com",
                Name = "n",
                Surname = "s",
                Password = "p"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class FailingRule : IRule<MDbContext>
    {
        public RuleType Type => RuleType.Business;

        public string Code => throw new NotImplementedException();

        public int Order => throw new NotImplementedException();

        public IReadOnlyList<string> DependsOn => throw new NotImplementedException();

        public HookPoint HookPoint => throw new NotImplementedException();

        public string Name => throw new NotImplementedException();

        public IEnumerable<Type> Dependencies => throw new NotImplementedException();

        public Task<RuleResult> EvaluateAsync(MDbContext ctx, FactBag facts, CancellationToken ct)
        {
            throw new NotImplementedException();
        }

        public Task ExecuteAsync(MDbContext context, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("fail");
        }
    }

    [Fact]
    public async Task ExecuteAsync_RollbackOnFailure()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<MDbContext> options = new DbContextOptionsBuilder<MDbContext>().UseSqlite(conn).Options;
        using MDbContext db = new(options, new FakeMediator());
        await db.Database.EnsureCreatedAsync();

        RuleEngine<MDbContext> engine = new RuleEngine<MDbContext>()
            .AddRule(new InsertUserRule())
            .AddRule(new FailingRule());

        await Assert.ThrowsAsync<MInternalException>(() => engine.ExecuteAsync(db));

        int count = await db.Users.CountAsync();
        Assert.Equal(0, count);
    }
}
