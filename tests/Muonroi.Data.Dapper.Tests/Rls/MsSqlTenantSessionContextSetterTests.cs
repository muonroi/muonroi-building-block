using FluentAssertions;
using Muonroi.Data.Dapper.Rls.Setters;
using Xunit;

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

        // Assert
        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].CommandText.Should().Contain("sp_set_session_context",
            "MSSQL uses sp_set_session_context to set the per-session tenant context");
        conn.ExecutedCommands[0].CommandText.Should().Contain("N'TenantId'",
            "the key literal N'TenantId' must appear in the command text");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("tenant-abc",
            "tenant id must be bound as the @value/@tid parameter");
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

        // Assert
        conn.ExecutedCommands.Should().ContainSingle();
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

        // Assert
        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].CommandText.Should().NotContain("DROP TABLE",
            "SQL injection must not appear in command text — only the @tid/@value placeholder is present");
        conn.ExecutedCommands[0].ParameterValue.Should().Be(malicious,
            "raw string is carried only as the bound parameter value (ADO.NET handles safe binding)");
    }

    // -------------------------------------------------------------------------
    // Test 4: Async path records the same single command as the sync path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ApplyAsync_WhenTenantSet_RecordsSameCommandAsSyncPath()
    {
        // Arrange
        SpyIMLog<MsSqlTenantSessionContextSetter> spy = new();
        MsSqlTenantSessionContextSetter sut = new(spy);
        FakeDbConnection conn = new();

        // Act
        await sut.ApplyAsync(conn, "tenant-abc", CancellationToken.None);

        // Assert
        conn.ExecutedCommands.Should().ContainSingle();
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
