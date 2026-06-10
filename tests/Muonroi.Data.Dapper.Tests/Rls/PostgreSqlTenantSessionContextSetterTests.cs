using FluentAssertions;
using Muonroi.Data.Dapper.Rls;
using Muonroi.Data.Dapper.Rls.Setters;
using Muonroi.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Muonroi.Data.Dapper.Tests.Rls;

/// <summary>
/// Tests for <see cref="PostgreSqlTenantSessionContextSetter"/> covering parameterization,
/// empty-string fallback, injection safety, sync/async parity, and OBS-01 logging.
/// </summary>
public sealed class PostgreSqlTenantSessionContextSetterTests
{
    // -------------------------------------------------------------------------
    // Test 1: Enabled + tenant set → one command, CommandText contains GUC, param == tenant id
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenTenantSet_ExecutesSetWithTenantId()
    {
        // Arrange
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, "tenant-abc");

        // Assert
        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].CommandText.Should().Contain("app.current_tenant_id");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("tenant-abc");
    }

    // -------------------------------------------------------------------------
    // Test 2: Enabled + null tenant → one command, parameter value == string.Empty
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenTenantNull_ExecutesSetWithEmptyString()
    {
        // Arrange
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, null);

        // Assert
        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].ParameterValue.Should().Be(string.Empty,
            "null tenant id must map to empty string so RLS blocks all rows downstream");
    }

    // -------------------------------------------------------------------------
    // Test 3: SQL-injection string → CommandText does NOT contain raw injection; bound as param value
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenSqlInjectionInTenantId_CommandTextDoesNotContainInjection()
    {
        // Arrange
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();
        const string malicious = "'; DROP TABLE x; --";

        // Act
        sut.Apply(conn, malicious);

        // Assert
        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].CommandText.Should().NotContain("DROP TABLE",
            "SQL injection must not appear in command text — only the @tid placeholder is present");
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
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();

        // Act
        await sut.ApplyAsync(conn, "tenant-abc", CancellationToken.None);

        // Assert
        conn.ExecutedCommands.Should().ContainSingle();
        conn.ExecutedCommands[0].CommandText.Should().Contain("app.current_tenant_id");
        conn.ExecutedCommands[0].ParameterValue.Should().Be("tenant-abc");
    }

    // -------------------------------------------------------------------------
    // Test 5: OBS-01 — null tenant → Warn called once, Info not called
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenTenantNull_WarnCalledOnce_InfoNotCalled()
    {
        // Arrange
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, null);

        // Assert
        spy.WarnCallCount.Should().Be(1, "Warn must be called when no tenant context is present (OBS-01)");
        spy.InfoCallCount.Should().Be(0, "Info must NOT be called when tenant is null");
    }

    // -------------------------------------------------------------------------
    // Test 6: OBS-01 — non-empty tenant → Info called once with tenant id, Warn not called
    // -------------------------------------------------------------------------

    [Fact]
    public void Apply_WhenTenantSet_InfoCalledOnce_WarnNotCalled()
    {
        // Arrange
        SpyIMLog<PostgreSqlTenantSessionContextSetter> spy = new();
        PostgreSqlTenantSessionContextSetter sut = new(bypassRoleName: "app_rls_bypass", log: spy);
        FakeDbConnection conn = new();

        // Act
        sut.Apply(conn, "tenant-xyz");

        // Assert
        spy.InfoCallCount.Should().Be(1, "Info must be called once when tenant id is present (OBS-01)");
        spy.WarnCallCount.Should().Be(0, "Warn must NOT be called when tenant is present");
    }
}

/// <summary>
/// Minimal spy implementation of <see cref="IMLog{T}"/> for asserting OBS-01 logging behavior.
/// Records call counts for Info and Warn; throws <see cref="NotImplementedException"/> for
/// members not under test.
/// </summary>
internal sealed class SpyIMLog<T> : IMLog<T>
{
    public int InfoCallCount { get; private set; }
    public int WarnCallCount { get; private set; }

    public void Info(string messageTemplate, params object?[] args) => InfoCallCount++;
    public void Warn(string messageTemplate, params object?[] args) => WarnCallCount++;
    public void Error(Exception? ex, string messageTemplate, params object?[] args) => throw new NotImplementedException();
    public void Debug(string messageTemplate, params object?[] args) => throw new NotImplementedException();
    public void InfoTrace(string messageTemplate, params object?[] args) => throw new NotImplementedException();
    public IMLogContextScope BeginProperty(string key, object? value) => throw new NotImplementedException();

    // ILogger<T> / ILogger members
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    public bool IsEnabled(LogLevel logLevel) => false;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
}
