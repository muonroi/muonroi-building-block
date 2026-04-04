namespace Muonroi.Data.EntityFrameworkCore.Entity.DataSample;

/// <summary>
/// Normalizes permission records after schema updates.
/// </summary>
/// <typeparam name="TContext">The EF Core context type.</typeparam>
/// <param name="context">The database context.</param>
public class PermissionDataMigrator<TContext>(TContext context)
    where TContext : MDbContext
{
    /// <summary>
    /// Updates existing permissions with default UI keys and types.
    /// </summary>
    public void Migrate()
    {
        List<MPermission> permissions = [.. context.Permissions];
        foreach (MPermission permission in permissions)
        {
            if (string.IsNullOrWhiteSpace(permission.UiKey)) permission.UiKey = permission.Name;
            permission.Type = PermissionType.Action;
        }

        _ = context.SaveChanges();
    }
}
