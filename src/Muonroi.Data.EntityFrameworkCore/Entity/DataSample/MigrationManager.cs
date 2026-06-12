using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Data.EntityFrameworkCore.Entity.DataSample;

/// <summary>
/// Provides database migration and seed helpers.
/// </summary>
public static class MigrationManager
{
    /// <summary>
    /// Ensures schema is created/migrated and seeds core data for the context.
    /// </summary>
    /// <typeparam name="TContext">The EF Core context type.</typeparam>
    /// <param name="app">The web application.</param>
    /// <returns>The same application instance for chaining.</returns>
    public static WebApplication MigrateDatabase<TContext>(this WebApplication app)
        where TContext : MDbContext
    {
        using IServiceScope scope = app.Services.CreateScope();
        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();
        IMLog<MDbContext>? logger = scope.ServiceProvider.GetService<IMLog<MDbContext>>();

        // 1. Ensure Schema Exists FIRST
        try
        {
            DatabaseConfigs? dbConfigs = scope.ServiceProvider.GetService<DatabaseConfigs>();
            bool isSqlite = string.Equals(dbConfigs?.DbType, "Sqlite", StringComparison.OrdinalIgnoreCase);

            logger?.Info("[MigrationManager] Ensuring database schema (DbType: {DbType})", dbConfigs?.DbType ?? "Unknown");

            if (isSqlite)
            {
                EnsureSqliteSchema(context, logger);
            }
            else
            {
                EnsureRelationalSchema(context, logger);
            }
        }
        catch (Exception ex)
        {
            logger?.Warn("[MigrationManager] Schema creation failed: {Message}. Attempting fallback...", ex.Message);
            try
            {
                context.Database.EnsureCreated();
                logger?.Info("[MigrationManager] Fallback EnsureCreated completed");
            }
            catch (Exception fallbackEx)
            {
                logger?.Error(fallbackEx, "[MigrationManager] Fallback also failed: {Message}", fallbackEx.Message);
            }
        }

        // 2. Sync Data SECOND (Requires tables to exist)
        try
        {
            IPermissionSyncService permissionSyncService = scope.ServiceProvider.GetRequiredService<IPermissionSyncService>();
            IMDateTimeService dateTimeService = scope.ServiceProvider.GetRequiredService<IMDateTimeService>();
            permissionSyncService.SyncPermissionsAsync().GetAwaiter().GetResult();

            new PermissionDataMigrator<TContext>(context).Migrate();
            new InitialHostDbBuilder<TContext>(context, dateTimeService).Create();
        }
        catch (Exception ex)
        {
            logger?.Warn("Data synchronization failed: {Message}. The application will continue without initial data.", ex.Message);
        }

        return app;
    }

    private static void EnsureSqliteSchema<TContext>(TContext context, IMLog<MDbContext>? logger)
        where TContext : MDbContext
    {
        // First, try to apply any pending migrations
        List<string> pendingMigrations = [.. context.Database.GetPendingMigrations()];
        if (pendingMigrations.Count > 0)
        {
            logger?.Info("[MigrationManager] Found {Count} pending migration(s). Applying...", pendingMigrations.Count);
            try
            {
                context.Database.Migrate();
                logger?.Info("[MigrationManager] Migrations applied successfully");
            }
            catch (Exception ex)
            {
                // Migrate may fail but tables could be created - check before falling back
                logger?.Warn("[MigrationManager] Migration error: {Message}", ex.Message);
                if (HasRequiredTables(context))
                {
                    logger?.Info("[MigrationManager] Tables exist despite error. Continuing...");
                }
                else
                {
                    throw; // Re-throw to trigger fallback
                }
            }
            return;
        }

        // Check if we have any migrations at all
        List<string> allMigrations = [.. context.Database.GetMigrations()];
        if (allMigrations.Count > 0)
        {
            // We have migrations but none pending - database should be up to date
            logger?.Info("[MigrationManager] Database has {Count} migration(s), all applied", allMigrations.Count);
            return;
        }

        // No migrations exist - fall back to EnsureCreated for development
        logger?.Info("[MigrationManager] No migrations found. Using EnsureCreated for development...");
        context.Database.EnsureCreated();
        logger?.Info("[MigrationManager] EnsureCreated completed");
    }

    private static bool HasRequiredTables<TContext>(TContext context) where TContext : MDbContext
    {
        try
        {
            IRelationalDatabaseCreator databaseCreator = context.GetService<IRelationalDatabaseCreator>();
            return databaseCreator.HasTables();
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureRelationalSchema<TContext>(TContext context, IMLog<MDbContext>? logger)
        where TContext : MDbContext
    {
        IRelationalDatabaseCreator databaseCreator = context.GetService<IRelationalDatabaseCreator>();

        if (context.Database.GetPendingMigrations().Any())
        {
            logger?.Info("[MigrationManager] Applying migrations...");
            context.Database.Migrate();
        }
        else if (databaseCreator != null && !databaseCreator.HasTables())
        {
            logger?.Info("[MigrationManager] Creating tables via EnsureCreated...");
            context.Database.EnsureCreated();
        }
    }
}
