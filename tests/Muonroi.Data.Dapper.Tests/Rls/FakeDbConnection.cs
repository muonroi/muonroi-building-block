using System.Data;
using System.Data.Common;

namespace Muonroi.Data.Dapper.Tests.Rls;

/// <summary>
/// Records the command text and first parameter value of each executed command.
/// </summary>
public sealed record ExecutedCommandInfo(string CommandText, string? ParameterValue);

/// <summary>
/// Minimal fake <see cref="DbConnection"/> that records commands created and executed against it.
/// Shared across all Dapper RLS setter tests.
/// </summary>
public sealed class FakeDbConnection : DbConnection
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
internal sealed class FakeDbCommand(List<ExecutedCommandInfo> log) : DbCommand
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

internal sealed class FakeDbParameterCollection : DbParameterCollection
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

internal sealed class FakeDbParameter : DbParameter
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
