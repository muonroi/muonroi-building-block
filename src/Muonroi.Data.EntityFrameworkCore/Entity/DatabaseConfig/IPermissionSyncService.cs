namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

/// <summary>
/// Synchronizes permission definitions into the persistence store.
/// </summary>
public interface IPermissionSyncService
{
    /// <summary>Synchronizes the current permission set.</summary>
    Task SyncPermissionsAsync();
}
