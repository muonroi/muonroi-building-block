namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

public interface IPermissionSyncService
{
    Task SyncPermissionsAsync();
}
