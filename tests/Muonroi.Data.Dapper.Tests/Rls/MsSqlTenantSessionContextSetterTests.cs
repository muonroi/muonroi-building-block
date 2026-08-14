namespace Muonroi.Data.Dapper.Tests.Rls;

/// <summary>
/// Tests for <see cref="MsSqlTenantSessionContextSetter"/> covering parameterization,
/// empty-string fallback, injection safety, sync/async parity, and OBS-01 logging.
/// </summary>
public sealed class MsSqlTenantSessionContextSetterTests
{
    // -------------------------------------------------------------------------
    // Test 1: Enabled + tenant set → one command, CommandText contains sp_set_session_context
    //         and N'TenantId' key; @value parameter == tenant id (not in CommandText)
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenTenantSet_ExecutesSpSetSessionContextWithTenantId()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, "tenant-abc");

        // Assert — normal path issues TWO commands (D-05): cmd[0] sets N'TenantId', cmd[1] clears N'TenantBypass'=0
        conn.ExecutedCommands.Should().HaveCount(2,
            "normal path issues two commands: N'TenantId' then N'TenantBypass'=0 (D-05)");
        conn.ExecutedCommands[0].CommandText.Should().Contain("sp_set_session_context",
            "MSSQL uses sp_set_session_context to set the per-session tenant context");
        conn.ExecutedCommands[0].CommandText.Should().Contain("N'TenantId'",
            "the key literal N'TenantId' must appear in the first command text");
        conn.ExecutedCommands[0].CommandText.Should().NotContain("read_only",
            "CR-02: @read_only=1 must NOT be set — re-setting a read-only key on a reused connection " +
            "throws SQL error 15664 under the set-per-open model");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("tenant-abc",
            "tenant id must be bound as the @value/@tid parameter");
    }

    // -------------------------------------------------------------------------
    // CR-02: Set-per-open compatibility — applying twice on the SAME connection
    //        must succeed (no @read_only=1 lock that would throw on re-set)
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_TwiceOnSameConnection_BothSucceed_NoReadOnlyLock()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act — set-per-open re-applies on every command against the SAME physical connection.
        sut.Apply(conn, "tenant-abc");
        sut.Apply(conn, "tenant-abc");

        // Assert — each normal Apply issues 2 commands (D-05), so two calls = 4 total.
        // No command carries @read_only, so SQL Server would not throw 15664.
        conn.ExecutedCommands.Should().HaveCount(4,
            "each normal Apply issues 2 commands (N'TenantId' + N'TenantBypass'=0); two calls = 4 total");
        conn.ExecutedCommands.Should().OnlyContain(c => !c.CommandText.Contains("read_only"),
            "no command may set @read_only=1 — that would break the second-and-later re-set");
    }

    [Fact]
    public async Task ApplyAsync_TwiceOnSameConnection_BothSucceed_NoReadOnlyLock()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        await sut.ApplyAsync(conn, "tenant-abc", CancellationToken.None);
        await sut.ApplyAsync(conn, "tenant-abc", CancellationToken.None);

        // Assert — each async normal Apply issues 2 commands (D-05), so two calls = 4 total.
        conn.ExecutedCommands.Should().HaveCount(4,
            "each async normal Apply issues 2 commands; two calls = 4 total");
        conn.ExecutedCommands.Should().OnlyContain(c => !c.CommandText.Contains("read_only"));
    }

    // -------------------------------------------------------------------------
    // Test 2: Null tenant → one command, parameter value == string.Empty
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenTenantNull_ExecutesSpSetSessionContextWithEmptyString()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, null);

        // Assert — two commands: cmd[0] sets N'TenantId'=empty, cmd[1] clears N'TenantBypass'=0
        conn.ExecutedCommands.Should().HaveCount(2,
            "normal path issues two commands even when tenant is null (D-05)");
        conn.ExecutedCommands[0].ParameterValue.Should().Be(string.Empty,
            "null tenant id must map to empty string so RLS blocks all rows downstream");
    }

    // -------------------------------------------------------------------------
    // Test 3: SQL-injection string → CommandText does NOT contain raw injection; bound as param
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenSqlInjectionInTenantId_CommandTextDoesNotContainInjection()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();
        const string malicious = "'; DROP TABLE x; --";

        // Act
        sut.Apply(conn, malicious);

        // Assert — two commands on normal path; injection must not appear in either command text
        conn.ExecutedCommands.Should().HaveCount(2,
            "normal path issues two commands (D-05)");
        conn.ExecutedCommands[0].CommandText.Should().NotContain("DROP TABLE",
            "SQL injection must not appear in command text — only the @tid/@value placeholder is present");
        conn.ExecutedCommands[0].ParameterValue.Should().Be(malicious,
            "raw string is carried only as the bound parameter value (ADO.NET handles safe binding)");
    }

    // -------------------------------------------------------------------------
    // Test 4: Async path records the same single command as the sync path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_WhenTenantSet_RecordsSameCommandsAsSyncPath()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        await sut.ApplyAsync(conn, "tenant-abc", CancellationToken.None);

        // Assert — async normal path also issues two commands (D-05)
        conn.ExecutedCommands.Should().HaveCount(2,
            "async normal path issues same two commands as sync path (D-05)");
        conn.ExecutedCommands[0].CommandText.Should().Contain("sp_set_session_context");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("tenant-abc");
    }

    // -------------------------------------------------------------------------
    // Test 5: OBS-01 — null tenant → Warn called once, Info not called
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenTenantNull_WarnCalledOnce_InfoNotCalled()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, null);

        // Assert
        spy.WarnCallCount.Should().Be(1, "Warn must be called when no tenant context is present (OBS-01)");
        spy.InfoCallCount.Should().Be(0, "Info must NOT be called when tenant is null");
    }

    // -------------------------------------------------------------------------
    // Test 6: OBS-01 — non-empty tenant → Info called once, Warn not called
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenTenantSet_InfoCalledOnce_WarnNotCalled()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, "tenant-xyz");

        // Assert
        spy.InfoCallCount.Should().Be(1, "Info must be called once when tenant id is present (OBS-01)");
        spy.WarnCallCount.Should().Be(0, "Warn must NOT be called when tenant is present");
    }
}
