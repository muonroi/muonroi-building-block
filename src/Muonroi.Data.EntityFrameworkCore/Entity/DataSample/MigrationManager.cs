using Muonroi.Core.Abstractions.Interfaces;

namespace Muonroi.Data.EntityFrameworkCore.Entity.DataSample;

public static class MigrationManager
{
    public static WebApplication MigrateDatabase<TContext>(this WebApplication app)
        where TContext : MDbContext
    {
        using IServiceScope scope = app.Services.CreateScope();
        TContext context = scope.ServiceProvider.GetRequiredService<TContext>();
        Microsoft.Extensions.Logging.ILogger? logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger(typeof(MigrationManager));

        // 1. Ensure Schema Exists FIRST
        try
        {
            DatabaseConfigs? dbConfigs = scope.ServiceProvider.GetService<DatabaseConfigs>();
            bool isSqlite = string.Equals(dbConfigs?.DbType, "Sqlite", StringComparison.OrdinalIgnoreCase);

            Console.WriteLine($"[MigrationManager] Ensuring database schema (DbType: {dbConfigs?.DbType ?? "Unknown"})...");

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
            Console.WriteLine($"[MigrationManager] Schema creation failed: {ex.Message}. Attempting fallback...");
            try
            {
                context.Database.EnsureCreated();
                Console.WriteLine("[MigrationManager] Fallback EnsureCreated completed.");
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"[MigrationManager] Fallback also failed: {fallbackEx.Message}");
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
            logger?.LogWarning("Data synchronization failed: {Message}. The application will continue without initial data.", ex.Message);
        }

        return app;
    }

    private static void EnsureSqliteSchema<TContext>(TContext context, Microsoft.Extensions.Logging.ILogger? logger)
        where TContext : MDbContext
    {
        // First, try to apply any pending migrations
        List<string> pendingMigrations = [.. context.Database.GetPendingMigrations()];
        if (pendingMigrations.Count > 0)
        {
            Console.WriteLine($"[MigrationManager] Found {pendingMigrations.Count} pending migration(s). Applying...");
            try
            {
                context.Database.Migrate();
                Console.WriteLine("[MigrationManager] Migrations applied successfully.");
            }
            catch (Exception ex)
            {
                // Migrate may fail but tables could be created - check before falling back
                Console.WriteLine($"[MigrationManager] Migration error: {ex.Message}");
                if (HasRequiredTables(context))
                {
                    Console.WriteLine("[MigrationManager] Tables exist despite error. Continuing...");
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
            Console.WriteLine($"[MigrationManager] Database has {allMigrations.Count} migration(s), all applied.");
            return;
        }

        // No migrations exist - fall back to EnsureCreated for development
        Console.WriteLine("[MigrationManager] No migrations found. Using EnsureCreated for development...");
        context.Database.EnsureCreated();
        Console.WriteLine("[MigrationManager] EnsureCreated completed.");
    }

    private static bool HasRequiredTables<TContext>(TContext context) where TContext : MDbContext
    {
        try
        {
            // Try to query from a core table to check if it exists
            _ = context.Database.ExecuteSqlRaw("SELECT 1 FROM MRoles LIMIT 1");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureRelationalSchema<TContext>(TContext context, Microsoft.Extensions.Logging.ILogger? logger)
        where TContext : MDbContext
    {
        IRelationalDatabaseCreator databaseCreator = context.GetService<IRelationalDatabaseCreator>();

        if (context.Database.GetPendingMigrations().Any())
        {
            Console.WriteLine("[MigrationManager] Applying migrations...");
            context.Database.Migrate();
        }
        else if (databaseCreator != null && !databaseCreator.HasTables())
        {
            Console.WriteLine("[MigrationManager] Creating tables via EnsureCreated...");
            context.Database.EnsureCreated();
        }
    }
}
