namespace Muonroi.Data.EntityFrameworkCore.Entity.DataSample;

public class PermissionDataMigrator<TContext>(TContext context)
    where TContext : MDbContext
{
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
