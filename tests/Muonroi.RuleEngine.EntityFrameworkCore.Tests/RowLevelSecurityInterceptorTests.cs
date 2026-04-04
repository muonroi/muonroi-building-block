using FluentAssertions;
using Microsoft.Extensions.Options;
using Muonroi.RuleEngine.EntityFrameworkCore.Rules;
using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Core;
using System.Data;
using System.Data.Common;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

/// <summary>
/// Tests for <see cref="TenantRlsConnectionInterceptor"/> covering all four RLS behaviors.
/// The internal Set*OnConnection methods are tested directly to avoid constructing EF Core
/// event data objects in tests.
/// </summary>
public sealed class RowLevelSecurityInterceptorTests : IDisposable
{
    public void Dispose()
    {
        // Reset ambient tenant context after each test to prevent test pollution.
        TenantContext.CurrentTenantId = null;
    }

    // -------------------------------------------------------------------------
    // Test 1: When EnableRowLevelSecurity=false, no SQL executed on connection open
    // -------------------------------------------------------------------------

    [Fact]
    public void SetTenantIdOnConnection_WhenRlsDisabled_DoesNotExecuteAnyCommand()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = false
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new();
        TenantContext.CurrentTenantId = "tenant-abc";

        // Act
        sut.SetTenantIdOnConnection(fakeConnection);

        // Assert
        fakeConnection.ExecutedCommands.Should().BeEmpty("SET command must not run when RLS is disabled");
    }

    [Fact]
    public async Task SetTenantIdOnConnectionAsync_WhenRlsDisabled_DoesNotExecuteAnyCommand()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = false
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new();
        TenantContext.CurrentTenantId = "tenant-abc";

        // Act
        await sut.SetTenantIdOnConnectionAsync(fakeConnection, CancellationToken.None);

        // Assert
        fakeConnection.ExecutedCommands.Should().BeEmpty("SET command must not run when RLS is disabled");
    }

    // -------------------------------------------------------------------------
    // Test 2: When RLS enabled and tenant is set, executes SET with correct tenant
    // -------------------------------------------------------------------------

    [Fact]
    public void SetTenantIdOnConnection_WhenRlsEnabled_AndTenantSet_ExecutesSetWithTenantId()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new();
        TenantContext.CurrentTenantId = "tenant-abc";

        // Act
        sut.SetTenantIdOnConnection(fakeConnection);

        // Assert
        fakeConnection.ExecutedCommands.Should().ContainSingle();
        fakeConnection.ExecutedCommands[0].CommandText.Should().Contain("app.current_tenant_id");
        fakeConnection.ExecutedCommands[0].ParameterValue.Should().Be("tenant-abc");
    }

    [Fact]
    public async Task SetTenantIdOnConnectionAsync_WhenRlsEnabled_AndTenantSet_ExecutesSetWithTenantId()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new();
        TenantContext.CurrentTenantId = "tenant-abc";

        // Act
        await sut.SetTenantIdOnConnectionAsync(fakeConnection, CancellationToken.None);

        // Assert
        fakeConnection.ExecutedCommands.Should().ContainSingle();
        fakeConnection.ExecutedCommands[0].CommandText.Should().Contain("app.current_tenant_id");
        fakeConnection.ExecutedCommands[0].ParameterValue.Should().Be("tenant-abc");
    }

    // -------------------------------------------------------------------------
    // Test 3: When RLS enabled and tenant is null, executes SET with empty string
    // -------------------------------------------------------------------------

    [Fact]
    public void SetTenantIdOnConnection_WhenRlsEnabled_AndTenantNull_ExecutesSetWithEmptyString()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new();
        TenantContext.CurrentTenantId = null;

        // Act
        sut.SetTenantIdOnConnection(fakeConnection);

        // Assert
        fakeConnection.ExecutedCommands.Should().ContainSingle();
        fakeConnection.ExecutedCommands[0].ParameterValue.Should().Be(string.Empty,
            "null tenant ID must map to empty string so PostgreSQL RLS blocks all rows");
    }

    [Fact]
    public async Task SetTenantIdOnConnectionAsync_WhenRlsEnabled_AndTenantNull_ExecutesSetWithEmptyString()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new();
        TenantContext.CurrentTenantId = null;

        // Act
        await sut.SetTenantIdOnConnectionAsync(fakeConnection, CancellationToken.None);

        // Assert
        fakeConnection.ExecutedCommands.Should().ContainSingle();
        fakeConnection.ExecutedCommands[0].ParameterValue.Should().Be(string.Empty,
            "null tenant ID must map to empty string so PostgreSQL RLS blocks all rows");
    }

    // -------------------------------------------------------------------------
    // Test 4: SQL injection attempt in tenant ID is safely parameterized
    // -------------------------------------------------------------------------

    [Fact]
    public void SetTenantIdOnConnection_WhenRlsEnabled_SqlInjectionInTenantId_IsParameterized()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new();
        string maliciousTenantId = "'; DROP TABLE \"RuleSets\"; --";
        TenantContext.CurrentTenantId = maliciousTenantId;

        // Act
        sut.SetTenantIdOnConnection(fakeConnection);

        // Assert: The command text contains a parameterized placeholder (@tid), not the raw injection string.
        fakeConnection.ExecutedCommands.Should().ContainSingle();
        fakeConnection.ExecutedCommands[0].CommandText.Should().NotContain("DROP TABLE",
            "SQL injection must not appear in command text — only a parameter placeholder is present");
        fakeConnection.ExecutedCommands[0].ParameterValue.Should().Be(maliciousTenantId,
            "raw tenant ID string is passed as a bound parameter value (ADO.NET handles safe binding)");
    }

    [Fact]
    public async Task SetTenantIdOnConnectionAsync_WhenRlsEnabled_SqlInjectionInTenantId_IsParameterized()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new();
        string maliciousTenantId = "'; DROP TABLE \"RuleSets\"; --";
        TenantContext.CurrentTenantId = maliciousTenantId;

        // Act
        await sut.SetTenantIdOnConnectionAsync(fakeConnection, CancellationToken.None);

        // Assert
        fakeConnection.ExecutedCommands.Should().ContainSingle();
        fakeConnection.ExecutedCommands[0].CommandText.Should().NotContain("DROP TABLE",
            "SQL injection must not appear in command text — only a parameter placeholder is present");
        fakeConnection.ExecutedCommands[0].ParameterValue.Should().Be(maliciousTenantId,
            "raw tenant ID string is passed as a bound parameter value (ADO.NET handles safe binding)");
    }

    // -------------------------------------------------------------------------
    // Fake DbConnection / DbCommand for test isolation
    // -------------------------------------------------------------------------

    private sealed record ExecutedCommandInfo(string CommandText, string? ParameterValue);

    /// <summary>
    /// Minimal fake <see cref="DbConnection"/> that records commands created and executed against it.
    /// </summary>
    private sealed class FakeDbConnection : DbConnection
    {
        public List<ExecutedCommandInfo> ExecutedCommands { get; } = new();

#pragma warning disable CS8765 // Nullability mismatch with base
        public override string ConnectionString { get; set; } = string.Empty;
#pragma warning restore CS8765
        public override string Database => string.Empty;
        public override string DataSource => string.Empty;
        public override string ServerVersion => string.Empty;
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();
        protected override DbCommand CreateDbCommand() => new FakeDbCommand(ExecutedCommands);
    }

    /// <summary>
    /// Minimal fake <see cref="DbCommand"/> that captures command text and first parameter value on execution.
    /// </summary>
    private sealed class FakeDbCommand(List<RowLevelSecurityInterceptorTests.ExecutedCommandInfo> log) : DbCommand
    {
        private readonly List<ExecutedCommandInfo> _log = log;
        private readonly FakeDbParameterCollection _parameters = new();

#pragma warning disable CS8765 // Nullability mismatch with base
        public override string CommandText { get; set; } = string.Empty;
#pragma warning restore CS8765
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override void Prepare() { }

        public override int ExecuteNonQuery()
        {
            string? firstParamValue = _parameters.Count > 0 ? _parameters[0].Value?.ToString() : null;
            _log.Add(new ExecutedCommandInfo(CommandText, firstParamValue));
            return 0;
        }

        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            string? firstParamValue = _parameters.Count > 0 ? _parameters[0].Value?.ToString() : null;
            _log.Add(new ExecutedCommandInfo(CommandText, firstParamValue));
            return await Task.FromResult(0);
        }

        public override object? ExecuteScalar() => null;
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotImplementedException();
        protected override DbParameter CreateDbParameter() => new FakeDbParameter();
    }

    private sealed class FakeDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _list = new();
        public override int Count => _list.Count;
        public override object SyncRoot => _list;
        public override int Add(object value) { _list.Add((DbParameter)value); return _list.Count - 1; }
        public override void AddRange(Array values) { foreach (object v in values) Add(v); }
        public override void Clear() => _list.Clear();
        public override bool Contains(object value) => _list.Contains((DbParameter)value);
        public override bool Contains(string value) => _list.Any(p => p.ParameterName == value);
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_list).CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _list.GetEnumerator();
        public override int IndexOf(object value) => _list.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _list.FindIndex(p => p.ParameterName == parameterName);
        public override void Insert(int index, object value) => _list.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _list.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _list.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            int i = IndexOf(parameterName);
            if (i >= 0) _list.RemoveAt(i);
        }
        protected override DbParameter GetParameter(int index) => _list[index];
        protected override DbParameter GetParameter(string parameterName) => _list.First(p => p.ParameterName == parameterName);
        protected override void SetParameter(int index, DbParameter value) => _list[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            int i = IndexOf(parameterName);
            if (i >= 0) _list[i] = value;
        }
    }

    private sealed class FakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
#pragma warning disable CS8765 // Nullability mismatch with base
        public override string ParameterName { get; set; } = string.Empty;
        public override string SourceColumn { get; set; } = string.Empty;
#pragma warning restore CS8765
        public override int Size { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }
}
