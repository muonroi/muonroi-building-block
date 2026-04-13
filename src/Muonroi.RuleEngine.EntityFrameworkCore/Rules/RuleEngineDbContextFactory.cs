using Microsoft.EntityFrameworkCore.Design;

namespace Muonroi.RuleEngine.EntityFrameworkCore.Rules;

/// <summary>
/// Design-time factory for <see cref="RuleEngineDbContext"/>.
/// </summary>
public sealed class RuleEngineDbContextFactory : IDesignTimeDbContextFactory<RuleEngineDbContext>
{
    /// <summary>
    /// Creates the design-time DbContext instance.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    public RuleEngineDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("MUONROI_RULEDB_CONNECTION")
            ?? "Host=localhost;Database=muonroi_rules;Username=admin;Password=admin";

        DbContextOptionsBuilder<RuleEngineDbContext> builder = new();
        builder.UseNpgsql(connectionString);
        return new RuleEngineDbContext(builder.Options);
    }
}
