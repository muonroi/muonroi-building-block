using Muonroi.Core.Abstractions.Guards;
using Muonroi.Tenancy.SiteProfile.Web.Dapper;

namespace Muonroi.Tenancy.SiteProfile.Web.DataAccess;

/// <summary>
/// Decorator that wraps an existing ISiteColumnMap (manual overrides) with EF-synced column data.
/// On Column() call: tries inner (manual) first — if inner returns the default convention value
/// AND a synced entry exists, returns the synced value instead. This ensures manual overrides
/// always take precedence (D-13/DATA-05).
/// </summary>
internal sealed class EfSyncedColumnMap : ISiteColumnMap
{
    private readonly ISiteColumnMap _inner;
    private readonly IReadOnlyDictionary<string, SyncedColumnInfo> _syncedEntries;
    private readonly DefaultSiteColumnMap _conventionFallback = new();

    public EfSyncedColumnMap(
        ISiteColumnMap inner,
        IReadOnlyDictionary<string, SyncedColumnInfo> syncedEntries)
    {
        _inner = MGuard.NotNull(inner);
        _syncedEntries = MGuard.NotNull(syncedEntries);
    }

    public string Column(string propertyName)
    {
        // Manual override takes precedence: if inner returns something different
        // from the default convention, it's a manual override — use it.
        string innerResult = _inner.Column(propertyName);
        string conventionResult = _conventionFallback.Column(propertyName);

        if (!string.Equals(innerResult, conventionResult, StringComparison.Ordinal))
            return innerResult; // Manual override — use as-is

        // Inner returned convention default — check if EF sync has a different value
        if (_syncedEntries.TryGetValue(propertyName, out var synced))
            return synced.ColumnName;

        return innerResult; // No synced entry — use convention
    }

    public string Column(string propertyName, string tableName)
    {
        string innerResult = _inner.Column(propertyName, tableName);
        string conventionResult = _conventionFallback.Column(propertyName);

        if (!string.Equals(innerResult, conventionResult, StringComparison.Ordinal))
            return innerResult;

        if (_syncedEntries.TryGetValue(propertyName, out var synced))
            return synced.ColumnName;

        return innerResult;
    }

    public bool HasColumn(string propertyName) => _inner.HasColumn(propertyName);

    public IReadOnlyList<SiteExtraColumn> ExtraColumns => _inner.ExtraColumns;

    /// <summary>
    /// Returns the synced EF metadata for a property, or null if not synced.
    /// Used by integration tests to verify DATA-04 (column name, max length, nullable).
    /// </summary>
    public SyncedColumnInfo? GetSyncedInfo(string propertyName)
        => _syncedEntries.TryGetValue(propertyName, out var info) ? info : null;
}
