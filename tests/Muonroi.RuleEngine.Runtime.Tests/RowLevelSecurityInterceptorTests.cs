using FluentAssertions;
using Microsoft.Extensions.Options;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.Tenancy.Abstractions;
using Muonroi.Tenancy.Core;
using NSubstitute;
using System.Data;
using System.Data.Common;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

/// <summary>
/// Tests for <see cref="TenantRlsConnectionInterceptor"/> covering all four RLS behaviors.
/// </summary>
public sealed class RowLevelSecurityInterceptorTests : IDisposable
{
    private readonly List<string> _executedCommands = new();

    public void Dispose()
    {
        // Reset ambient tenant context after each test to prevent test pollution.
        TenantContext.CurrentTenantId = null;
    }

    // -------------------------------------------------------------------------
    // Test 1: When EnableRowLevelSecurity=false, no SQL executed on connection open
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConnectionOpenedAsync_WhenRlsDisabled_DoesNotExecuteSetCommand()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = false
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new(_executedCommands);
        TenantContext.CurrentTenantId = "tenant-abc";

        // Act
        await sut.ConnectionOpenedAsync(fakeConnection, new ConnectionEndEventData(
            eventDefinition: null!,
            messageGenerator: null!,
            connection: fakeConnection,
            connectionId: Guid.NewGuid(),
            startTime: DateTimeOffset.UtcNow,
            duration: TimeSpan.Zero,
            async: true),
            cancellationToken: CancellationToken.None);

        // Assert
        _executedCommands.Should().BeEmpty("SET command must not run when RLS is disabled");
    }

    [Fact]
    public void ConnectionOpened_WhenRlsDisabled_DoesNotExecuteSetCommand()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = false
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new(_executedCommands);
        TenantContext.CurrentTenantId = "tenant-abc";

        // Act
        sut.ConnectionOpened(fakeConnection, new ConnectionEndEventData(
            eventDefinition: null!,
            messageGenerator: null!,
            connection: fakeConnection,
            connectionId: Guid.NewGuid(),
            startTime: DateTimeOffset.UtcNow,
            duration: TimeSpan.Zero,
            async: false));

        // Assert
        _executedCommands.Should().BeEmpty("SET command must not run when RLS is disabled");
    }

    // -------------------------------------------------------------------------
    // Test 2: When RLS enabled and tenant is set, executes SET with correct tenant
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConnectionOpenedAsync_WhenRlsEnabled_AndTenantSet_ExecutesSetWithTenantId()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new(_executedCommands);
        TenantContext.CurrentTenantId = "tenant-abc";

        // Act
        await sut.ConnectionOpenedAsync(fakeConnection, new ConnectionEndEventData(
            eventDefinition: null!,
            messageGenerator: null!,
            connection: fakeConnection,
            connectionId: Guid.NewGuid(),
            startTime: DateTimeOffset.UtcNow,
            duration: TimeSpan.Zero,
            async: true),
            cancellationToken: CancellationToken.None);

        // Assert
        _executedCommands.Should().ContainSingle();
        _executedCommands[0].Should().Contain("app.current_tenant_id");
        fakeConnection.LastParameterValue.Should().Be("tenant-abc");
    }

    [Fact]
    public void ConnectionOpened_WhenRlsEnabled_AndTenantSet_ExecutesSetWithTenantId()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new(_executedCommands);
        TenantContext.CurrentTenantId = "tenant-abc";

        // Act
        sut.ConnectionOpened(fakeConnection, new ConnectionEndEventData(
            eventDefinition: null!,
            messageGenerator: null!,
            connection: fakeConnection,
            connectionId: Guid.NewGuid(),
            startTime: DateTimeOffset.UtcNow,
            duration: TimeSpan.Zero,
            async: false));

        // Assert
        _executedCommands.Should().ContainSingle();
        _executedCommands[0].Should().Contain("app.current_tenant_id");
        fakeConnection.LastParameterValue.Should().Be("tenant-abc");
    }

    // -------------------------------------------------------------------------
    // Test 3: When RLS enabled and tenant is null, executes SET with empty string
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConnectionOpenedAsync_WhenRlsEnabled_AndTenantNull_ExecutesSetWithEmptyString()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new(_executedCommands);
        TenantContext.CurrentTenantId = null;

        // Act
        await sut.ConnectionOpenedAsync(fakeConnection, new ConnectionEndEventData(
            eventDefinition: null!,
            messageGenerator: null!,
            connection: fakeConnection,
            connectionId: Guid.NewGuid(),
            startTime: DateTimeOffset.UtcNow,
            duration: TimeSpan.Zero,
            async: true),
            cancellationToken: CancellationToken.None);

        // Assert
        _executedCommands.Should().ContainSingle();
        fakeConnection.LastParameterValue.Should().Be(string.Empty,
            "null tenant ID must map to empty string so RLS blocks all rows");
    }

    [Fact]
    public void ConnectionOpened_WhenRlsEnabled_AndTenantNull_ExecutesSetWithEmptyString()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new(_executedCommands);
        TenantContext.CurrentTenantId = null;

        // Act
        sut.ConnectionOpened(fakeConnection, new ConnectionEndEventData(
            eventDefinition: null!,
            messageGenerator: null!,
            connection: fakeConnection,
            connectionId: Guid.NewGuid(),
            startTime: DateTimeOffset.UtcNow,
            duration: TimeSpan.Zero,
            async: false));

        // Assert
        _executedCommands.Should().ContainSingle();
        fakeConnection.LastParameterValue.Should().Be(string.Empty,
            "null tenant ID must map to empty string so RLS blocks all rows");
    }

    // -------------------------------------------------------------------------
    // Test 4: SQL injection safety — tenant ID with quotes uses parameterized SET
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConnectionOpenedAsync_WhenRlsEnabled_SqlInjectionInTenantId_IsParameterized()
    {
        // Arrange
        IOptions<MultiTenantOptions> options = Options.Create(new MultiTenantOptions
        {
            EnableRowLevelSecurity = true
        });
        TenantRlsConnectionInterceptor sut = new(options);
        FakeDbConnection fakeConnection = new(_executedCommands);
        string maliciousTenantId = "'; DROP TABLE \"RuleSets\"; --";
        TenantContext.CurrentTenantId = maliciousTenantId;

        // Act
        await sut.ConnectionOpenedAsync(fakeConnection, new ConnectionEndEventData(
            eventDefinition: null!,
            messageGenerator: null!,
            connection: fakeConnection,
            connectionId: Guid.NewGuid(),
            startTime: DateTimeOffset.UtcNow,
            duration: TimeSpan.Zero,
            async: true),
            cancellationToken: CancellationToken.None);

        // Assert: The malicious string is passed as a parameter value (not embedded in SQL text).
        // The command text must NOT contain the raw injection string.
        _executedCommands.Should().ContainSingle();
        _executedCommands[0].Should().NotContain("DROP TABLE",
            "SQL injection must be neutralized by parameterization");
        fakeConnection.LastParameterValue.Should().Be(maliciousTenantId,
            "parameter value must be the raw tenant string (ADO.NET handles escaping)");
    }

    // -------------------------------------------------------------------------
    // Fake DbConnection for test isolation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Minimal fake <see cref="DbConnection"/> that records executed command texts and parameter values.
    /// </summary>
    private sealed class FakeDbConnection : DbConnection
    {
        private readonly List<string> _log;
        public string? LastParameterValue { get; private set; }

        public FakeDbConnection(List<string> log) => _log = log;

#pragma warning disable CS8765
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

        protected override DbCommand CreateDbCommand() => new FakeDbCommand(this, _log, v => LastParameterValue = v);
    }

    /// <summary>
    /// Minimal fake <see cref="DbCommand"/> that captures the SET command text and parameter values.
    /// </summary>
    private sealed class FakeDbCommand : DbCommand
    {
        private readonly FakeDbConnection _connection;
        private readonly List<string> _log;
        private readonly Action<string?> _captureParam;
        private readonly FakeDbParameterCollection _parameters = new();

        public FakeDbCommand(FakeDbConnection connection, List<string> log, Action<string?> captureParam)
        {
            _connection = connection;
            _log = log;
            _captureParam = captureParam;
        }

#pragma warning disable CS8765
        public override string CommandText { get; set; } = string.Empty;
#pragma warning restore CS8765
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
#pragma warning disable CS8765
        protected override DbConnection? DbConnection { get => _connection; set { } }
#pragma warning restore CS8765
        protected override DbParameterCollection DbParameterCollection => _parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override void Prepare() { }
        public override int ExecuteNonQuery()
        {
            _log.Add(CommandText);
            if (_parameters.Count > 0)
            {
                _captureParam(_parameters[0].Value?.ToString());
            }
            return 0;
        }

        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            _log.Add(CommandText);
            if (_parameters.Count > 0)
            {
                _captureParam(_parameters[0].Value?.ToString());
            }
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
#pragma warning disable CS8765 // Nullability mismatch — base declares non-nullable in older target
        public override string ParameterName { get; set; } = string.Empty;
        public override string SourceColumn { get; set; } = string.Empty;
#pragma warning restore CS8765
        public override int Size { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }
}
