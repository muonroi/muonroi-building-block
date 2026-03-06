using Microsoft.EntityFrameworkCore.Design;

namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class RuleEngineDbContextFactory : IDesignTimeDbContextFactory<RuleEngineDbContext>
{
    public RuleEngineDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("MUONROI_RULEDB_CONNECTION")
            ?? "Host=localhost;Database=muonroi_rules;Username=admin;Password=admin";

        DbContextOptionsBuilder<RuleEngineDbContext> builder = new();
        builder.UseNpgsql(connectionString);
        return new RuleEngineDbContext(builder.Options);
    }
}
