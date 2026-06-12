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
        conn.ExecutedCommands[0].CommandText.Should().Contain("RESET ROLE",
            "the normal path must RESET ROLE so a reused pooled connection is re-isolated (CR-03)");
        conn.ExecutedCommands[0].CommandText.Should().NotContain("SET ROLE app",
            "the normal path must not SET ROLE to the bypass role");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("tenant-abc");
    }

    [Fact]
    public void Apply_WhenBypassNotActive_IssuesResetRoleBeforeGuc()
    {
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();

        sut.Apply(conn, "tenant-abc");

        conn.ExecutedCommands.Should().ContainSingle();
        string text = conn.ExecutedCommands[0].CommandText;
        text.Should().Contain("RESET ROLE");
        text.IndexOf("RESET ROLE", StringComparison.Ordinal)
            .Should().BeLessThan(text.IndexOf("app.current_tenant_id", StringComparison.Ordinal),
                "RESET ROLE must precede the GUC set so a bypass-elevated connection is re-isolated before the tenant predicate is applied (CR-03)");
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
        conn.ExecutedCommands[0].CommandText.Should().Contain("RESET ROLE");
        conn.ExecutedCommands[0].CommandText.Should().NotContain("SET ROLE app");
    }

    [Fact]
    public void Constructor_WhenBypassRoleNameNull_Throws()
    {
        Action act = () => _ = new PostgreSqlTenantSessionContextSetter(bypassRoleName: null!);
        act.Should().Throw<Exception>("MGuard.NotEmpty rejects a null bypass role name");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("app rls")]              // space
    [InlineData("app_rls; DROP TABLE x")] // semicolon / injection-shaped
    [InlineData("1app_rls")]             // leading digit
    [InlineData("app-rls")]              // hyphen
    [InlineData("\"app_rls\"")]          // quotes
    public void Constructor_WhenBypassRoleNameNotSafeIdentifier_Throws(string roleName)
    {
        Action act = () => _ = new PostgreSqlTenantSessionContextSetter(bypassRoleName: roleName);
        act.Should().Throw<Exception>(
            "WR-01: BypassRoleName must be a well-formed unquoted SQL identifier for SET ROLE");
    }

    [Theory]
    [InlineData("app_rls_bypass")]
    [InlineData("custom_bypass")]
    [InlineData("_role1")]
    public void Constructor_WhenBypassRoleNameIsSafeIdentifier_DoesNotThrow(string roleName)
    {
        Action act = () => _ = new PostgreSqlTenantSessionContextSetter(bypassRoleName: roleName);
        act.Should().NotThrow();
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
