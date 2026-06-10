using FluentAssertions;
using Muonroi.Data.Dapper.Rls.Bypass;
using Muonroi.Data.Dapper.Rls.Setters;
using Xunit;

namespace Muonroi.Data.Dapper.Tests.Rls;

/// <summary>
/// Tests for the bypass set/clear extension of <see cref="MsSqlTenantSessionContextSetter"/>
/// introduced in plan 03-03 (D-05):
/// <list type="bullet">
/// <item>Bypass path: sets N'TenantBypass'=1 via one DbCommand, emits IMLog.Warn, does NOT set N'TenantId'.</item>
/// <item>Normal path: sets N'TenantId' via cmd1, then clears N'TenantBypass'=0 via a SECOND separate DbCommand.</item>
/// <item>No @read_only in any path (D-07).</item>
/// </list>
/// </summary>
public sealed class MsSqlTenantSessionContextSetterBypassTests
{
    // -------------------------------------------------------------------------
    // Bypass path — sync Apply
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenBypassActive_IssuesOnlyTenantBypassCmd_AndWarns()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        using (DapperRlsBypass.Enter())
        {
            sut.Apply(conn, "tenant-abc");
        }

        // Assert — exactly ONE command, carries N'TenantBypass' key with value 1
        conn.ExecutedCommands.Should().ContainSingle(
            "bypass path must issue exactly one sp_set_session_context call for N'TenantBypass'");
        conn.ExecutedCommands[0].CommandText.Should().Contain("N'TenantBypass'",
            "bypass command must set the N'TenantBypass' session-context key");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("1",
            "bypass value must be 1 (elevation active)");
        conn.ExecutedCommands[0].CommandText.Should().NotContain("N'TenantId'",
            "bypass path must NOT set N'TenantId' — the predicate must not match a tenant on bypass");
        spy.WarnCallCount.Should().Be(1,
            "every bypassed connection open must emit IMLog.Warn (BYP-01 audit)");
    }

    [Fact]
    public void Apply_WhenBypassActive_NoReadOnlyInCommand()
    {
        // Arrange
        MsSqlTenantSessionContextSetter sut = new();
        FakeDbConnection conn = new();

        // Act
        using (DapperRlsBypass.Enter())
        {
            sut.Apply(conn, "tenant-abc");
        }

        // Assert — D-07: @read_only must not appear on any path
        conn.ExecutedCommands.Should().OnlyContain(c => !c.CommandText.Contains("read_only"),
            "D-07: @read_only=1 must not be sent on any command");
    }

    // -------------------------------------------------------------------------
    // Bypass path — async ApplyAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_WhenBypassActive_IssuesOnlyTenantBypassCmd_AndWarns()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        using (DapperRlsBypass.Enter())
        {
            await sut.ApplyAsync(conn, "tenant-abc", CancellationToken.None);
        }

        // Assert
        conn.ExecutedCommands.Should().ContainSingle(
            "async bypass path must issue exactly one command");
        conn.ExecutedCommands[0].CommandText.Should().Contain("N'TenantBypass'");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("1");
        conn.ExecutedCommands[0].CommandText.Should().NotContain("N'TenantId'",
            "async bypass path must NOT set N'TenantId'");
        spy.WarnCallCount.Should().Be(1,
            "async bypass path must emit IMLog.Warn");
    }

    [Fact]
    public async Task ApplyAsync_WhenBypassActive_NoReadOnlyInCommand()
    {
        MsSqlTenantSessionContextSetter sut = new();
        FakeDbConnection conn = new();

        using (DapperRlsBypass.Enter())
        {
            await sut.ApplyAsync(conn, "tenant-abc", CancellationToken.None);
        }

        conn.ExecutedCommands.Should().OnlyContain(c => !c.CommandText.Contains("read_only"));
    }

    // -------------------------------------------------------------------------
    // Normal path — TWO separate DbCommand executions (Pitfall 3 / D-05)
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenBypassNotActive_TenantSet_IssuesTwoCommands()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, "tenant-xyz");

        // Assert — two commands: cmd[0] sets N'TenantId', cmd[1] clears N'TenantBypass'=0
        conn.ExecutedCommands.Should().HaveCount(2,
            "normal path must issue TWO separate sp_set_session_context commands (Pitfall 3 / D-05)");
        conn.ExecutedCommands[0].CommandText.Should().Contain("N'TenantId'",
            "first command sets the tenant id context");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("tenant-xyz",
            "first command carries the tenant id as a bound parameter");
        conn.ExecutedCommands[1].CommandText.Should().Contain("N'TenantBypass'",
            "second command clears the bypass flag (pooled-connection elevation leak prevention)");
        conn.ExecutedCommands[1].ParameterValue.Should().Be("0",
            "bypass flag must be cleared to 0 on every normal open");
    }

    [Fact]
    public void Apply_WhenBypassNotActive_NullTenant_IssuesTwoCommandsWithEmptyString()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, null);

        // Assert
        conn.ExecutedCommands.Should().HaveCount(2,
            "normal path issues two commands even when tenant is null");
        conn.ExecutedCommands[0].CommandText.Should().Contain("N'TenantId'");
        conn.ExecutedCommands[0].ParameterValue.Should().Be(string.Empty,
            "null tenant maps to empty string on the N'TenantId' command");
        conn.ExecutedCommands[1].CommandText.Should().Contain("N'TenantBypass'");
        conn.ExecutedCommands[1].ParameterValue.Should().Be("0");
    }

    [Fact]
    public async Task ApplyAsync_WhenBypassNotActive_TenantSet_IssuesTwoCommands()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        await sut.ApplyAsync(conn, "tenant-xyz", CancellationToken.None);

        // Assert
        conn.ExecutedCommands.Should().HaveCount(2,
            "async normal path must also issue two separate commands");
        conn.ExecutedCommands[0].CommandText.Should().Contain("N'TenantId'");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("tenant-xyz");
        conn.ExecutedCommands[1].CommandText.Should().Contain("N'TenantBypass'");
        conn.ExecutedCommands[1].ParameterValue.Should().Be("0");
    }

    [Fact]
    public void Apply_WhenBypassNotActive_NoReadOnlyInAnyCommand()
    {
        // Arrange
        MsSqlTenantSessionContextSetter sut = new();
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, "tenant-abc");

        // Assert — D-07: no command may carry @read_only
        conn.ExecutedCommands.Should().OnlyContain(c => !c.CommandText.Contains("read_only"),
            "D-07: @read_only=1 must not appear in any normal-path command");
    }

    // -------------------------------------------------------------------------
    // Pooled-connection elevation leak prevention (TST-04 analogue at unit level)
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_AfterBypassScope_NormalOpenClearsBypassFlag()
    {
        // Arrange: simulate a connection that was previously used in bypass scope.
        // The setter's normal path must clear N'TenantBypass'=0 regardless.
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act — bypass open first, then normal open on the same connection instance
        using (DapperRlsBypass.Enter())
        {
            sut.Apply(conn, "tenant-abc");
        }
        sut.Apply(conn, "tenant-abc"); // normal open on reused connection

        // Assert — bypass command + then TWO normal commands (TenantId + TenantBypass=0)
        conn.ExecutedCommands.Should().HaveCount(3,
            "1 bypass command + 2 normal-path commands on the second open");
        conn.ExecutedCommands[2].CommandText.Should().Contain("N'TenantBypass'",
            "normal open must clear bypass flag to 0 (pooled-connection elevation leak prevention)");
        conn.ExecutedCommands[2].ParameterValue.Should().Be("0");
    }
}
