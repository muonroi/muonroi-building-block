using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Muonroi.RuleEngine.Runtime.Rules;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleEngineDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WhenEnvironmentVariableMissing_ShouldUseFallbackConnectionString()
    {
        string? original = Environment.GetEnvironmentVariable("MUONROI_RULEDB_CONNECTION");

        try
        {
            Environment.SetEnvironmentVariable("MUONROI_RULEDB_CONNECTION", null);

            RuleEngineDbContextFactory factory = new();
            using RuleEngineDbContext context = factory.CreateDbContext([]);

            context.Database.ProviderName.Should().Be("Npgsql.EntityFrameworkCore.PostgreSQL");
            context.Database.GetConnectionString().Should().Be("Host=localhost;Database=muonroi_rules;Username=admin;Password=admin");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MUONROI_RULEDB_CONNECTION", original);
        }
    }

    [Fact]
    public void CreateDbContext_WhenEnvironmentVariableProvided_ShouldUseCustomConnectionString()
    {
        string? original = Environment.GetEnvironmentVariable("MUONROI_RULEDB_CONNECTION");
        const string expected = "Host=db.internal;Database=rules;Username=tester;Password=secret";

        try
        {
            Environment.SetEnvironmentVariable("MUONROI_RULEDB_CONNECTION", expected);

            RuleEngineDbContextFactory factory = new();
            using RuleEngineDbContext context = factory.CreateDbContext([]);

            context.Database.GetConnectionString().Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MUONROI_RULEDB_CONNECTION", original);
        }
    }
}
