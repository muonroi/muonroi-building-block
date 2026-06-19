using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Tenancy.SiteProfile.Web.DataAccess;

/// <summary>
/// IHostedService that runs at startup when <c>SyncColumnMappingFromEfModel: true</c>.
/// Discovers all site DbContext types via source-generated <c>SiteDbContextTypeRegistry</c>
/// (in namespace <c>Muonroi.Tenancy.SiteProfile.Generated</c>), creates a temporary instance
/// of each, reads IModel metadata, and stores extracted column info (name, max length, nullable)
/// per property keyed by DbContext full type name.
///
/// The synced data is consumed by <see cref="EfSyncedColumnMap"/> which decorates the
/// existing <see cref="Dapper.ISiteColumnMap"/> chain.
/// </summary>
internal sealed class EfColumnSyncHostedService(
    IServiceProvider serviceProvider,
    ILogger<EfColumnSyncHostedService> logger) : IHostedService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<EfColumnSyncHostedService> _logger = logger;

    // Static store — populated once at startup, read by EfSyncedColumnMap instances.
    // Key: DbContext full type name (e.g., "MyApp.AlphaSiteDbContext")
    // Value: dictionary of propertyName -> SyncedColumnInfo
    private static readonly Dictionary<string, Dictionary<string, SyncedColumnInfo>> _syncedMaps = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Use the source-generated SiteDbContextTypeRegistry to discover all site DbContexts.
        // The type lives in the consumer's assembly under namespace:
        //   Muonroi.Tenancy.SiteProfile.Generated
        // We cannot reference it directly — use reflection to discover it.
        Type? registryType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return []; }
            })
            .FirstOrDefault(t =>
                t.Name == "SiteDbContextTypeRegistry" &&
                t.Namespace == "Muonroi.Tenancy.SiteProfile.Generated" &&
                t.GetMethod("GetAllSiteDbContextTypes") != null);

        if (registryType is null)
        {
            _logger.LogWarning(
                "SiteDbContextTypeRegistry not found in any loaded assembly. " +
                "EF column sync skipped. " +
                "Ensure at least one [GenerateSiteProfile] attribute is declared.");
            return Task.CompletedTask;
        }

        var method = MGuard.NotNull(registryType.GetMethod("GetAllSiteDbContextTypes"));
        var result = method.Invoke(null, null);

        if (result is not IReadOnlyList<Type> dbContextTypes || dbContextTypes.Count == 0)
        {
            _logger.LogInformation("SiteDbContextTypeRegistry returned no DbContext types. EF column sync skipped.");
            return Task.CompletedTask;
        }

        using var scope = _serviceProvider.CreateScope();

        foreach (var dbContextType in dbContextTypes)
        {
            try
            {
                SyncForContext(scope.ServiceProvider, dbContextType);
            }
            catch (Exception ex)
            {
                // D-09: fail-fast if EF config is broken
                _logger.LogError(ex,
                    "EF column sync failed for DbContext '{DbContextType}'. " +
                    "Fix EF configuration or disable SyncColumnMappingFromEfModel.",
                    dbContextType.Name);
                throw;
            }
        }

        _logger.LogInformation("EF column sync completed for {Count} DbContext(s).", _syncedMaps.Count);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void SyncForContext(IServiceProvider sp, Type dbContextType)
    {
        // Create temporary DbContext to read IModel
        if (sp.GetService(dbContextType) is not DbContext dbContext)
        {
            _logger.LogWarning(
                "Could not resolve DbContext type '{Type}' from service provider. " +
                "EF column sync skipped for this DbContext.",
                dbContextType.Name);
            return;
        }

        using (dbContext)
        {
            var model = dbContext.Model;
            var entries = new Dictionary<string, SyncedColumnInfo>(StringComparer.Ordinal);

            foreach (var entityType in model.GetEntityTypes())
            {
                // Skip owned entities — they share the owner's table
                if (entityType.IsOwned()) continue;

                var tableName = entityType.GetTableName();
                if (tableName is null) continue; // TPH child without its own table

                var storeObject = StoreObjectIdentifier.Table(
                    tableName, entityType.GetSchema());

                foreach (var property in entityType.GetProperties())
                {
                    if (property.IsShadowProperty()) continue;

                    var columnName = property.GetColumnName(storeObject);
                    if (columnName is null) continue;

                    var maxLength = property.GetMaxLength();
                    var isNullable = property.IsNullable;

                    // Use property name as key — last entity wins for shared property names.
                    // This is acceptable: same property should map to same column in well-configured models.
                    entries[property.Name] = new SyncedColumnInfo(columnName, maxLength, isNullable);
                }
            }

            string key = dbContextType.FullName ?? dbContextType.Name;
            _syncedMaps[key] = entries;
            _logger.LogDebug(
                "Synced {Count} column mappings for DbContext '{DbContextType}'.",
                entries.Count, dbContextType.Name);
        }
    }

    /// <summary>
    /// Returns the synced column map entries for a given DbContext type.
    /// Called by DI registration (Plan 03) to construct <see cref="EfSyncedColumnMap"/> instances.
    /// </summary>
    /// <param name="dbContextType">The site DbContext type to look up.</param>
    /// <returns>
    /// Synced property-to-column entries, or an empty dictionary if sync has not run or the
    /// DbContext was not discovered.
    /// </returns>
    internal static IReadOnlyDictionary<string, SyncedColumnInfo> GetSyncedEntries(Type dbContextType)
    {
        string key = dbContextType.FullName ?? dbContextType.Name;
        return _syncedMaps.TryGetValue(key, out var entries)
            ? entries
            : new Dictionary<string, SyncedColumnInfo>();
    }
}
