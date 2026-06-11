using System.Data.Common;
using System.Reflection;
using Dapper;
using Dapper.Extensions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Data.Dapper.Rls;
using Npgsql;
using Xunit;

namespace Muonroi.Data.Dapper.Tests.Rls;

/// <summary>
/// Reflection-based coverage test: asserts that every public virtual Query* / Execute*
/// overload on <see cref="BaseDapper{TConn}"/> is overridden in
/// <see cref="TenantRlsDapper{TConn}"/>.
///
/// Failing assertion messages name the missing overload so a future Dapper.Extensions
/// package upgrade that adds a new method is caught by CI with an actionable signal.
/// </summary>
public sealed class TenantRlsDapperOverrideCoverageTests
{
    [Fact(DisplayName = "TenantRlsDapper overrides ALL public virtual Query*/Execute* methods from BaseDapper")]
    public void AllPublicVirtualQueryExecuteMethodsAreOverridden()
    {
        // Arrange
        var baseDapperType = typeof(BaseDapper<NpgsqlConnection>);
        var rlsDapperType = typeof(TenantRlsDapper<NpgsqlConnection>);

        // Enumerate every public instance virtual method on BaseDapper whose name starts with
        // "Query" or "Execute" and that is not sealed (final).
        var baseMethods = baseDapperType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.IsVirtual && !m.IsFinal)
            .Where(m => m.Name.StartsWith("Query", StringComparison.Ordinal)
                     || m.Name.StartsWith("Execute", StringComparison.Ordinal))
            .ToList();

        // The 5.3.1 surface has 110 overloads; guard against the enum going below 20.
        baseMethods.Should().HaveCountGreaterThanOrEqualTo(20,
            because: "Dapper.Extensions 5.3.1 exposes >=20 Query*/Execute* overloads; if this drops the DLL version may have changed");

        // Act + Assert: each base method must be overridden in TenantRlsDapper.
        var missing = new List<string>();

        foreach (var baseMethod in baseMethods)
        {
            // Match by name and parameter signature (type + position).
            var baseParams = baseMethod.GetParameters();

            // For generic methods we look for a matching generic method by name + arity + param types.
            var candidates = rlsDapperType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.Name == baseMethod.Name)
                .Where(m => m.GetParameters().Length == baseParams.Length);

            bool found = candidates.Any(rlsMethod =>
            {
                var rlsParams = rlsMethod.GetParameters();
                // Compare parameter types by their generic-erased form (MetadataToken is not reliable
                // across assemblies; compare type full name with generic arity markers).
                for (int i = 0; i < baseParams.Length; i++)
                {
                    if (baseParams[i].ParameterType.FullName != rlsParams[i].ParameterType.FullName
                        && baseParams[i].ParameterType.Name != rlsParams[i].ParameterType.Name)
                    {
                        return false;
                    }
                }
                return true;
            });

            if (!found)
            {
                var sig = FormatSignature(baseMethod);
                missing.Add(sig);
            }
        }

        missing.Should().BeEmpty(
            because: "TenantRlsDapper must override EVERY BaseDapper Query*/Execute* overload. " +
                     "Missing overrides (add them to TenantRlsDapper.cs):\n  " +
                     string.Join("\n  ", missing));
    }

    private static string FormatSignature(MethodInfo m)
    {
        var parms = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
        return $"{m.ReturnType.Name} {m.Name}({parms})";
    }
}

/// <summary>
/// Behavioral guard-routing tests (closes CR-01 / IN-04): the reflection coverage test above
/// proves each async (CommandDefinition) overload is overridden, but cannot detect an async
/// override that mistakenly invokes the SYNCHRONOUS guard. These tests drive each async
/// (CommandDefinition) overload through the public surface and assert it routed through the
/// ASYNC guard (ApplyAsync called once, Apply NOT called) — catching the exact sync-over-async
/// regression CR-01 flagged on ExecuteAsync / ExecuteReaderAsync / ExecuteScalarAsync /
/// QueryAsync (non-generic) / QueryMultipleAsync.
/// </summary>
public sealed class TenantRlsDapperAsyncGuardRoutingTests
{
    private static IServiceProvider BuildMinimalSp()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:test"] = "Host=localhost;Database=testdb;Username=test;Password=test"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IConnectionStringProvider, TestConnectionStringProvider>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static TestableTenantRlsDapper NewSut(SpyITenantSessionContextSetter spy)
        => new(BuildMinimalSp(), spy, new SpyITenantContext("tenant-async"));

    [Fact(DisplayName = "ExecuteAsync(CommandDefinition) routes through the ASYNC guard (CR-01)")]
    public async Task ExecuteAsync_CommandDefinition_UsesAsyncGuard()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = NewSut(spy);

        _ = await Record.ExceptionAsync(() => sut.ExecuteAsync(new CommandDefinition("SELECT 1")));

        spy.ApplyAsyncCallCount.Should().Be(1,
            because: "ExecuteAsync(CommandDefinition) must await EnsureTenantContextAsync (no sync-over-async)");
        spy.ApplyCallCount.Should().Be(0,
            because: "the async overload must NOT call the synchronous Apply guard");
    }

    [Fact(DisplayName = "ExecuteReaderAsync(CommandDefinition) routes through the ASYNC guard (CR-01)")]
    public async Task ExecuteReaderAsync_CommandDefinition_UsesAsyncGuard()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = NewSut(spy);

        _ = await Record.ExceptionAsync(() => sut.ExecuteReaderAsync(new CommandDefinition("SELECT 1")));

        spy.ApplyAsyncCallCount.Should().Be(1,
            because: "ExecuteReaderAsync(CommandDefinition) must await EnsureTenantContextAsync");
        spy.ApplyCallCount.Should().Be(0);
    }

    [Fact(DisplayName = "ExecuteScalarAsync<T>(CommandDefinition) routes through the ASYNC guard (CR-01)")]
    public async Task ExecuteScalarAsync_CommandDefinition_UsesAsyncGuard()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = NewSut(spy);

        _ = await Record.ExceptionAsync(() => sut.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1")));

        spy.ApplyAsyncCallCount.Should().Be(1,
            because: "ExecuteScalarAsync<T>(CommandDefinition) must await EnsureTenantContextAsync");
        spy.ApplyCallCount.Should().Be(0);
    }

    [Fact(DisplayName = "QueryAsync(CommandDefinition) non-generic routes through the ASYNC guard (CR-01)")]
    public async Task QueryAsync_CommandDefinition_NonGeneric_UsesAsyncGuard()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = NewSut(spy);

        _ = await Record.ExceptionAsync(() => sut.QueryAsync(new CommandDefinition("SELECT 1")));

        spy.ApplyAsyncCallCount.Should().Be(1,
            because: "non-generic QueryAsync(CommandDefinition) must await EnsureTenantContextAsync");
        spy.ApplyCallCount.Should().Be(0);
    }

    [Fact(DisplayName = "QueryMultipleAsync(CommandDefinition, Action) routes through the ASYNC guard (CR-01)")]
    public async Task QueryMultipleAsync_CommandDefinition_UsesAsyncGuard()
    {
        var spy = new SpyITenantSessionContextSetter();
        var sut = NewSut(spy);

        _ = await Record.ExceptionAsync(() => sut.QueryMultipleAsync(new CommandDefinition("SELECT 1"), _ => { }));

        spy.ApplyAsyncCallCount.Should().Be(1,
            because: "QueryMultipleAsync(CommandDefinition, Action) must await EnsureTenantContextAsync");
        spy.ApplyCallCount.Should().Be(0);
    }
}
