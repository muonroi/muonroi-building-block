using System.Data;
using System.Data.Common;
using Dapper;
using Dapper.Extensions;
using Dapper.Extensions.SQL;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;
using Muonroi.Tenancy.Abstractions;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Muonroi.Data.Dapper.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Muonroi.Data.Dapper.PostgreSql.IntegrationTests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Muonroi.Data.Dapper.MsSql.IntegrationTests")]

namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// A <see cref="BaseDapper{TConn}"/> subclass that enforces tenant Row-Level Security by
/// running the configured <see cref="ITenantSessionContextSetter"/> against the physical
/// connection BEFORE every Query or Execute call, on both sync and async paths.
/// </summary>
/// <typeparam name="TConn">
/// The concrete ADO.NET <see cref="DbConnection"/> type (e.g. <c>NpgsqlConnection</c>).
/// Must have a public parameterless constructor — required by <see cref="BaseDapper{TConn}"/>.
/// </typeparam>
/// <remarks>
/// <para>
/// This class overrides EVERY public virtual <c>Query*</c> / <c>Execute*</c> method on
/// <see cref="BaseDapper{TConn}"/> (Dapper.Extensions.NetCore 5.3.1, 110 overloads) through a
/// single guard pair (<see cref="EnsureTenantContext"/> / <see cref="EnsureTenantContextAsync"/>)
/// so there is no silent cross-tenant leak path. A reflection coverage test enforces this
/// invariant for future package upgrades.
/// </para>
/// <para>
/// The guard re-runs on EVERY call (set-per-open, not set-once-per-Lazy), which is required
/// because pooled connections can be reused across requests and must be re-scoped on each
/// acquisition (HOOK-05, T-02-02).
/// </para>
/// <para>
/// NOTE — ZeeLyn 5.3.1 limitation (RESEARCH Pitfall 2): <see cref="BaseDapper{TConn}"/>
/// uses a synchronous <c>conn.Open()</c> inside its private <c>CreateConnection</c> factory;
/// no <c>OpenAsync</c> exists. Accessing <c>Conn.Value</c> on async overrides therefore
/// causes a synchronous physical open even though the subsequent SET round-trip is properly
/// awaited via <see cref="ITenantSessionContextSetter.ApplyAsync"/>.
/// </para>
/// </remarks>
/// <remarks>
/// Initializes a new instance of <see cref="TenantRlsDapper{TConn}"/>.
/// </remarks>
/// <param name="serviceProvider">Service provider — forwarded to <see cref="BaseDapper{TConn}"/> for cache/profiler resolution.</param>
/// <param name="connectionName">Named connection string key.</param>
/// <param name="enableMasterSlave">Enable master/slave routing.</param>
/// <param name="readOnly">Route to the read-only replica when true.</param>
/// <param name="setter">The provider-specific tenant session context setter (not null).</param>
/// <param name="tenantContext">The ambient tenant context (not null).</param>
/// <param name="strictMode">
/// When <see langword="true"/>, <see cref="EnsureTenantContext"/> and
/// <see cref="EnsureTenantContextAsync"/> will throw <c>MissingTenantContextException</c>
/// if the ambient tenant id is absent and no bypass scope is active (HARD-03 / D-08).
/// Defaults to <see langword="false"/> — behavior is byte-identical to v1.0 when off.
/// </param>
/// <param name="log">Optional structured logger for observability.</param>
public class TenantRlsDapper<TConn>(
    IServiceProvider serviceProvider,
    string connectionName,
    bool enableMasterSlave,
    bool readOnly,
    ITenantSessionContextSetter setter,
    ITenantContext tenantContext,
    bool strictMode = false,
    IMLog<TenantRlsDapper<TConn>>? log = null) : BaseDapper<TConn>(serviceProvider, connectionName, enableMasterSlave, readOnly)
    where TConn : DbConnection, new()
{
    private readonly ITenantSessionContextSetter _setter = MGuard.NotNull(setter);
    private readonly ITenantContext _tenantContext = MGuard.NotNull(tenantContext);
    private readonly IMLog<TenantRlsDapper<TConn>>? _log = log;
    private readonly bool _strictMode = strictMode;

    // -------------------------------------------------------------------------
    // Guard pair — the single choke point for all sync and async paths.
    // Both guards are internal (not private) so the test project can drive them
    // directly via InternalsVisibleTo without exposing them on the public API.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Synchronously ensures the current tenant context is set on the physical connection
    /// before any Dapper command executes. Re-runs on every call (set-per-open).
    /// </summary>
    internal void EnsureTenantContext()
    {
        // HARD-03: strict-mode guard — fail loud before the setter runs.
        // Guard fires only when strict-mode is on, the tenant id is absent,
        // AND no sanctioned bypass scope is active (D-07).
        // The strict-off path is byte-identical to v1.0 (criterion #3, Pitfall 5).
        var tenantId = _tenantContext.TenantId;
        if (_strictMode
            && string.IsNullOrWhiteSpace(tenantId)
            && !Bypass.DapperRlsBypass.IsActive)
        {
            throw new MissingTenantContextException();
        }

        // Accessing Conn.Value triggers BaseDapper's synchronous Open() if not yet open.
        var conn = (DbConnection)Conn.Value;
        _setter.Apply(conn, tenantId);
    }

    /// <summary>
    /// Asynchronously ensures the current tenant context is set on the physical connection.
    /// NOTE: Conn.Value triggers a synchronous Open() (ZeeLyn 5.3.1 limitation);
    /// the SET round-trip is properly awaited.
    /// </summary>
    internal async Task EnsureTenantContextAsync(CancellationToken ct = default)
    {
        // HARD-03: strict-mode guard — same logic as the sync path (D-07, criterion #3).
        var tenantId = _tenantContext.TenantId;
        if (_strictMode
            && string.IsNullOrWhiteSpace(tenantId)
            && !Bypass.DapperRlsBypass.IsActive)
        {
            throw new MissingTenantContextException();
        }

        // Accessing Conn.Value triggers BaseDapper's synchronous Open() (ZeeLyn 5.3.1 limitation).
        var conn = (DbConnection)Conn.Value;
        await _setter.ApplyAsync(conn, tenantId, ct).ConfigureAwait(false);
    }

    // =========================================================================
    // Execute overrides
    // =========================================================================

    /// <inheritdoc />
    public override int Execute(string sql, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.Execute(sql, param, commandTimeout, commandType);
    }

    /// <inheritdoc />
    public override int Execute(SQLName name, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.Execute(name, param, commandTimeout, commandType);
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(CommandDefinition command)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.ExecuteAsync(command).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(string sql, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.ExecuteAsync(sql, param, commandTimeout, commandType).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteAsync(SQLName name, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.ExecuteAsync(name, param, commandTimeout, commandType).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override IDataReader ExecuteReader(string sql, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.ExecuteReader(sql, param, commandTimeout, commandType);
    }

    /// <inheritdoc />
    public override IDataReader ExecuteReader(SQLName name, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.ExecuteReader(name, param, commandTimeout, commandType);
    }

    /// <inheritdoc />
    public override async Task<IDataReader> ExecuteReaderAsync(CommandDefinition command)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.ExecuteReaderAsync(command).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<IDataReader> ExecuteReaderAsync(string sql, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.ExecuteReaderAsync(sql, param, commandTimeout, commandType).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<IDataReader> ExecuteReaderAsync(SQLName name, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.ExecuteReaderAsync(name, param, commandTimeout, commandType).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override TReturn ExecuteScalar<TReturn>(string sql, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.ExecuteScalar<TReturn>(sql, param, commandTimeout, commandType);
    }

    /// <inheritdoc />
    public override TReturn ExecuteScalar<TReturn>(SQLName name, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.ExecuteScalar<TReturn>(name, param, commandTimeout, commandType);
    }

    /// <inheritdoc />
    public override async Task<TReturn> ExecuteScalarAsync<TReturn>(CommandDefinition command)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.ExecuteScalarAsync<TReturn>(command).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<TReturn> ExecuteScalarAsync<TReturn>(string sql, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.ExecuteScalarAsync<TReturn>(sql, param, commandTimeout, commandType).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<TReturn> ExecuteScalarAsync<TReturn>(SQLName name, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.ExecuteScalarAsync<TReturn>(name, param, commandTimeout, commandType).ConfigureAwait(false);
    }

    // =========================================================================
    // Query overrides — non-generic (object return)
    // =========================================================================

    /// <inheritdoc />
    public override List<object> Query(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    /// <inheritdoc />
    public override List<object> Query(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    // =========================================================================
    // Query overrides — generic TReturn
    // =========================================================================

    /// <inheritdoc />
    public override List<TReturn> Query<TReturn>(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TReturn>(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    /// <inheritdoc />
    public override List<TReturn> Query<TReturn>(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TReturn>(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    // =========================================================================
    // Query multi-map overrides (2 types)
    // =========================================================================

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TReturn>(string sql, Func<TFirst, TSecond, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TReturn>(SQLName name, Func<TFirst, TSecond, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    // =========================================================================
    // Query multi-map overrides (3 types)
    // =========================================================================

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TThird, TReturn>(string sql, Func<TFirst, TSecond, TThird, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TThird, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TThird, TReturn>(SQLName name, Func<TFirst, TSecond, TThird, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TThird, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    // =========================================================================
    // Query multi-map overrides (4 types)
    // =========================================================================

    /// <inheritdoc />
    public override List<TResult> Query<TFirst, TSecond, TThird, TFourth, TResult>(string sql, Func<TFirst, TSecond, TThird, TFourth, TResult> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TThird, TFourth, TResult>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(SQLName name, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TThird, TFourth, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    // =========================================================================
    // Query multi-map overrides (5 types)
    // =========================================================================

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(SQLName name, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    // =========================================================================
    // Query multi-map overrides (6 types)
    // =========================================================================

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(SQLName name, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    // =========================================================================
    // Query multi-map overrides (7 types)
    // =========================================================================

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    /// <inheritdoc />
    public override List<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(SQLName name, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true)
    {
        EnsureTenantContext();
        return base.Query<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered);
    }

    // =========================================================================
    // QueryAsync overrides — non-generic and generic
    // =========================================================================

    /// <inheritdoc />
    public override async Task<List<object>> QueryAsync(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryAsync(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TReturn>(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryAsync<TReturn>(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<object>> QueryAsync(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<object>> QueryAsync(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TReturn>(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TReturn>(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TReturn>(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TReturn>(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryAsync multi-map overrides (2 types)
    // =========================================================================

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TReturn>(CommandDefinition command, Func<TFirst, TSecond, TReturn> map, string splitOn = "Id", bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TReturn>(command, map, splitOn, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TReturn>(string sql, Func<TFirst, TSecond, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TReturn>(SQLName name, Func<TFirst, TSecond, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryAsync multi-map overrides (3 types)
    // =========================================================================

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TReturn>(CommandDefinition command, Func<TFirst, TSecond, TThird, TReturn> map, string splitOn = "Id", bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TReturn>(command, map, splitOn, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TReturn>(string sql, Func<TFirst, TSecond, TThird, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TReturn>(SQLName name, Func<TFirst, TSecond, TThird, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryAsync multi-map overrides (4 types)
    // =========================================================================

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TReturn>(CommandDefinition command, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, string splitOn = "Id", bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TReturn>(command, map, splitOn, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TReturn>(SQLName name, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryAsync multi-map overrides (5 types)
    // =========================================================================

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(CommandDefinition command, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, string splitOn = "Id", bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(command, map, splitOn, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(SQLName name, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryAsync multi-map overrides (6 types)
    // =========================================================================

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(CommandDefinition command, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn> map, string splitOn = "Id", bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(command, map, splitOn, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(SQLName name, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryAsync multi-map overrides (7 types)
    // =========================================================================

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(CommandDefinition command, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn> map, string splitOn = "Id", bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(command, map, splitOn, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(sql, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(SQLName name, Func<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn> map, object param = null!, string splitOn = "Id", int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, bool buffered = true, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryAsync<TFirst, TSecond, TThird, TFourth, TFifth, TSixth, TSeventh, TReturn>(name, map, param, splitOn, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, buffered, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryFirst overrides
    // =========================================================================

    /// <inheritdoc />
    public override async Task<TReturn> QueryFirstAsync<TReturn>(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryFirstAsync<TReturn>(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryFirstOrDefault overrides
    // =========================================================================

    /// <inheritdoc />
    public override TReturn QueryFirstOrDefault<TReturn>(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.QueryFirstOrDefault<TReturn>(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType);
    }

    /// <inheritdoc />
    public override TReturn QueryFirstOrDefault<TReturn>(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.QueryFirstOrDefault<TReturn>(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType);
    }

    /// <inheritdoc />
    public override object QueryFirstOrDefault(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.QueryFirstOrDefault(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType);
    }

    /// <inheritdoc />
    public override object QueryFirstOrDefault(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.QueryFirstOrDefault(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType);
    }

    /// <inheritdoc />
    public override async Task<TReturn> QueryFirstOrDefaultAsync<TReturn>(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryFirstOrDefaultAsync<TReturn>(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<object> QueryFirstOrDefaultAsync(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryFirstOrDefaultAsync(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<TReturn> QueryFirstOrDefaultAsync<TReturn>(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryFirstOrDefaultAsync<TReturn>(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<TReturn> QueryFirstOrDefaultAsync<TReturn>(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryFirstOrDefaultAsync<TReturn>(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<object> QueryFirstOrDefaultAsync(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryFirstOrDefaultAsync(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<object> QueryFirstOrDefaultAsync(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryFirstOrDefaultAsync(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryMultiple overrides
    // =========================================================================

    /// <inheritdoc />
    public override void QueryMultiple(string sql, Action<SqlMapper.GridReader> reader, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        EnsureTenantContext();
        base.QueryMultiple(sql, reader, param, commandTimeout, commandType);
    }

    /// <inheritdoc />
    public override void QueryMultiple(SQLName name, Action<SqlMapper.GridReader> reader, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        EnsureTenantContext();
        base.QueryMultiple(name, reader, param, commandTimeout, commandType);
    }

    /// <inheritdoc />
    public override async Task QueryMultipleAsync(CommandDefinition command, Action<SqlMapper.GridReader> reader)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        await base.QueryMultipleAsync(command, reader).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task QueryMultipleAsync(string sql, Action<SqlMapper.GridReader> reader, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        await base.QueryMultipleAsync(sql, reader, param, commandTimeout, commandType).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task QueryMultipleAsync(SQLName name, Action<SqlMapper.GridReader> reader, object param = null!, int? commandTimeout = null, CommandType? commandType = null)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        await base.QueryMultipleAsync(name, reader, param, commandTimeout, commandType).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2)> QueryMultipleAsync<T1, T2>(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2>(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2, List<T3> Result3)> QueryMultipleAsync<T1, T2, T3>(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2, T3>(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2, List<T3> Result3, List<T4> Result4)> QueryMultipleAsync<T1, T2, T3, T4>(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2, T3, T4>(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2, List<T3> Result3, List<T4> Result4, List<T5> Result5)> QueryMultipleAsync<T1, T2, T3, T4, T5>(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2, T3, T4, T5>(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2)> QueryMultipleAsync<T1, T2>(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2>(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2)> QueryMultipleAsync<T1, T2>(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2>(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2, List<T3> Result3)> QueryMultipleAsync<T1, T2, T3>(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2, T3>(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2, List<T3> Result3)> QueryMultipleAsync<T1, T2, T3>(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2, T3>(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2, List<T3> Result3, List<T4> Result4)> QueryMultipleAsync<T1, T2, T3, T4>(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2, T3, T4>(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2, List<T3> Result3, List<T4> Result4)> QueryMultipleAsync<T1, T2, T3, T4>(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2, T3, T4>(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2, List<T3> Result3, List<T4> Result4, List<T5> Result5)> QueryMultipleAsync<T1, T2, T3, T4, T5>(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2, T3, T4, T5>(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<(List<T1> Result1, List<T2> Result2, List<T3> Result3, List<T4> Result4, List<T5> Result5)> QueryMultipleAsync<T1, T2, T3, T4, T5>(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryMultipleAsync<T1, T2, T3, T4, T5>(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryPage overrides
    // =========================================================================

    /// <inheritdoc />
    public override PageResult<TReturn> QueryPage<TReturn>(SQLName name, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        EnsureTenantContext();
        return base.QueryPage<TReturn>(name, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache);
    }

    /// <inheritdoc />
    public override PageResult<object> QueryPage(SQLName name, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        EnsureTenantContext();
        return base.QueryPage(name, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache);
    }

    /// <inheritdoc />
    public override PageResult<TReturn> QueryPage<TReturn>(string countSql, string dataSql, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        EnsureTenantContext();
        return base.QueryPage<TReturn>(countSql, dataSql, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache);
    }

    /// <inheritdoc />
    public override PageResult<object> QueryPage(string countSql, string dataSql, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        EnsureTenantContext();
        return base.QueryPage(countSql, dataSql, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache);
    }

    // =========================================================================
    // QueryPageAsync overrides
    // =========================================================================

    /// <inheritdoc />
    public override async Task<PageResult<TReturn>> QueryPageAsync<TReturn>(SQLName name, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryPageAsync<TReturn>(name, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<PageResult<object>> QueryPageAsync(SQLName name, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryPageAsync(name, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<PageResult<TReturn>> QueryPageAsync<TReturn>(string countSql, string dataSql, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryPageAsync<TReturn>(countSql, dataSql, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<PageResult<object>> QueryPageAsync(string countSql, string dataSql, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryPageAsync(countSql, dataSql, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QueryPlainPage overrides
    // =========================================================================

    /// <inheritdoc />
    public override List<TReturn> QueryPlainPage<TReturn>(string sql, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        EnsureTenantContext();
        return base.QueryPlainPage<TReturn>(sql, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache);
    }

    /// <inheritdoc />
    public override List<TReturn> QueryPlainPage<TReturn>(SQLName name, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        EnsureTenantContext();
        return base.QueryPlainPage<TReturn>(name, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache);
    }

    /// <inheritdoc />
    public override List<object> QueryPlainPage(string sql, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        EnsureTenantContext();
        return base.QueryPlainPage(sql, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache);
    }

    /// <inheritdoc />
    public override List<object> QueryPlainPage(SQLName name, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        EnsureTenantContext();
        return base.QueryPlainPage(name, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache);
    }

    // =========================================================================
    // QueryPlainPageAsync overrides
    // =========================================================================

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryPlainPageAsync<TReturn>(string sql, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryPlainPageAsync<TReturn>(sql, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<TReturn>> QueryPlainPageAsync<TReturn>(SQLName name, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryPlainPageAsync<TReturn>(name, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<object>> QueryPlainPageAsync(string sql, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryPlainPageAsync(sql, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<List<object>> QueryPlainPageAsync(SQLName name, int pageindex, int pageSize, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QueryPlainPageAsync(name, pageindex, pageSize, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, cancellationToken).ConfigureAwait(false);
    }

    // =========================================================================
    // QuerySingle overrides
    // =========================================================================

    /// <inheritdoc />
    public override async Task<TReturn> QuerySingleAsync<TReturn>(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QuerySingleAsync<TReturn>(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    // =========================================================================
    // QuerySingleOrDefault overrides
    // =========================================================================

    /// <inheritdoc />
    public override object QuerySingleOrDefault(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.QuerySingleOrDefault(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType);
    }

    /// <inheritdoc />
    public override object QuerySingleOrDefault(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.QuerySingleOrDefault(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType);
    }

    /// <inheritdoc />
    public override TReturn QuerySingleOrDefault<TReturn>(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.QuerySingleOrDefault<TReturn>(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType);
    }

    /// <inheritdoc />
    public override TReturn QuerySingleOrDefault<TReturn>(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null)
    {
        EnsureTenantContext();
        return base.QuerySingleOrDefault<TReturn>(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType);
    }

    /// <inheritdoc />
    public override async Task<TReturn> QuerySingleOrDefaultAsync<TReturn>(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QuerySingleOrDefaultAsync<TReturn>(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<object> QuerySingleOrDefaultAsync(CommandDefinition command, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false)
    {
        await EnsureTenantContextAsync().ConfigureAwait(false);
        return await base.QuerySingleOrDefaultAsync(command, enableCache, cacheExpire, cacheKey, forceUpdateCache).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<object> QuerySingleOrDefaultAsync(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QuerySingleOrDefaultAsync(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<object> QuerySingleOrDefaultAsync(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QuerySingleOrDefaultAsync(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<TReturn> QuerySingleOrDefaultAsync<TReturn>(string sql, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QuerySingleOrDefaultAsync<TReturn>(sql, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task<TReturn> QuerySingleOrDefaultAsync<TReturn>(SQLName name, object param = null!, int? commandTimeout = null, bool? enableCache = null, TimeSpan? cacheExpire = null, string cacheKey = null!, bool forceUpdateCache = false, CommandType? commandType = null, CancellationToken cancellationToken = default)
    {
        await EnsureTenantContextAsync(cancellationToken).ConfigureAwait(false);
        return await base.QuerySingleOrDefaultAsync<TReturn>(name, param, commandTimeout, enableCache, cacheExpire, cacheKey, forceUpdateCache, commandType, cancellationToken).ConfigureAwait(false);
    }
}
