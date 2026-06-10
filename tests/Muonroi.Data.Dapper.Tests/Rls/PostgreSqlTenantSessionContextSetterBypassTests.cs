using FluentAssertions;
using Muonroi.Data.Dapper.Rls;
using Muonroi.Data.Dapper.Rls.Bypass;
using Muonroi.Data.Dapper.Rls.Setters;
using Xunit;

namespace Muonroi.Data.Dapper.Tests.Rls;

/// <summary>
/// Tests for the cross-tenant bypass branch of <see cref="PostgreSqlTenantSessionContextSetter"/>:
/// when <see cref="DapperRlsBypass"/> is active the setter must issue <c>SET ROLE</c> + Warn and
/// skip the per-tenant GUC; when inactive the existing GUC path runs unchanged.
/// </summary>
public sealed class PostgreSqlTenantSessionContextSetterBypassTests
{
    [Fact]
    public void Apply_WhenBypassActive_IssuesSetRoleAndWarns()
    {
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();

        using (DapperRlsBypass.Enter())
        {
            sut.Apply(conn, "tenant-abc");
        }

        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].CommandText.Should().Be("SET ROLE app_rls_bypass");
        conn.ExecutedCommands[0].CommandText.Should().NotContain("app.current_tenant_id",
            "the bypass path must NOT issue the per-tenant GUC");
        spy.WarnCallCount.Should().Be(1, "every bypassed connection open is audit-logged (D-06)");
    }

    [Fact]
    public void Apply_WhenBypassActiveWithCustomRole_IssuesConfiguredRole()
    {
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "custom_bypass", log: spy);
        FakeDbConnection conn = new();

        using (DapperRlsBypass.Enter())
        {
            sut.Apply(conn, "tenant-abc");
        }

        conn.ExecutedCommands[0].CommandText.Should().Be("SET ROLE custom_bypass");
    }

    [Fact]
    public async Task ApplyAsync_WhenBypassActive_IssuesSetRoleAndWarns()
    {
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();

        using (DapperRlsBypass.Enter())
        {
            await sut.ApplyAsync(conn, "tenant-abc", CancellationToken.None);
        }

        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].CommandText.Should().Be("SET ROLE app_rls_bypass");
        spy.WarnCallCount.Should().Be(1);
    }

    [Fact]
    public void Apply_WhenBypassNotActive_IssuesGucUnchanged()
    {
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();

        sut.Apply(conn, "tenant-abc");

        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].CommandText.Should().Contain("app.current_tenant_id",
            "without an active bypass scope the existing GUC path runs unchanged");
        conn.ExecutedCommands[0].CommandText.Should().NotContain("SET ROLE");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("tenant-abc");
    }

    [Fact]
    public async Task ApplyAsync_WhenBypassNotActive_IssuesGucUnchanged()
    {
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();

        await sut.ApplyAsync(conn, "tenant-abc", CancellationToken.None);

        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].CommandText.Should().Contain("app.current_tenant_id");
        conn.ExecutedCommands[0].CommandText.Should().NotContain("SET ROLE");
    }

    [Fact]
    public void Constructor_WhenBypassRoleNameNull_Throws()
    {
        Action act = () => _ = new PostgreSqlTenantSessionContextSetter(bypassRoleName: null!);
        act.Should().Throw<Exception>("MGuard.NotNull rejects a null bypass role name");
    }

    [Fact]
    public void Options_BypassRoleName_DefaultsToAppRlsBypass()
    {
        DapperRlsOptions opts = new();
        opts.BypassRoleName.Should().Be("app_rls_bypass");
    }

    [Fact]
    public void Options_BypassRoleName_IsConfigurable()
    {
        DapperRlsOptions opts = new() { BypassRoleName = "custom_bypass" };
        opts.BypassRoleName.Should().Be("custom_bypass");
    }
}
