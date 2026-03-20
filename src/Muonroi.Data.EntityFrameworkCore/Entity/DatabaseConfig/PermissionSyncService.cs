namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

/// <summary>
/// Synchronizes permission definitions from providers and enums into the database.
/// </summary>
public class PermissionSyncService<TDbContext>(TDbContext context, IEnumerable<IPermissionProvider> providers)
    : IPermissionSyncService
    where TDbContext : MDbContext
{
    /// <summary>Synchronizes the current permission set.</summary>
    public async Task SyncPermissionsAsync()
    {
        Assembly assembly = typeof(TDbContext).Assembly;

        List<MPermission> permissionsFromEnum =
        [
            .. assembly.GetTypes()
                .Where(t => t.IsEnum && t.Name.Contains("Permission"))
                .SelectMany(enumType => Enum.GetValues(enumType).Cast<Enum>())
                .Where(e => Convert.ToInt64(e) != 0)
                .Select(e =>
                {
                    MPermission permission = new() { Name = e.ToString(),
                    IsGranted = true,
                    Discriminator = "Permission",
                    UiKey = e.ToString(),
                    Type = PermissionType.Action };
                    return permission;
                })
        ];

        permissionsFromEnum = [.. permissionsFromEnum
            .GroupBy(p => p.Name)
            .Select(g => g.First())];

        List<MPermission> existingPermissions = await context.Permissions.ToListAsync();

        List<MPermission> newPermissions =
            [.. permissionsFromEnum.Where(p => existingPermissions.All(ep => ep.Name != p.Name))];

        foreach (IPermissionProvider provider in providers)
        {
            IEnumerable<PermissionDefinition> defs = provider.GetPermissions();
            foreach (PermissionDefinition def in defs)
            {
                MPermissionGroup? group = null;
                if (!string.IsNullOrWhiteSpace(def.GroupName))
                {
                    group = await context.PermissionGroups.FirstOrDefaultAsync(g => g.Name == def.GroupName);
                    if (group == null)
                    {
                        group = new MPermissionGroup
                        {
                            Name = def.GroupName,
                            DisplayName = def.GroupDisplayName
                        };
                        await context.PermissionGroups.AddAsync(group);
                        await context.SaveChangesAsync();
                    }
                }

                foreach (string permName in def.Permissions)
                    if (existingPermissions.All(p => p.Name != permName) && newPermissions.All(p => p.Name != permName))
                    {
                        MPermission perm = new()
                        {
                            Name = permName,
                            IsGranted = true,
                            Discriminator = "Permission",
                            UiKey = permName,
                            Type = PermissionType.Action,
                            PermissionGroup = group
                        };
                        newPermissions.Add(perm);
                    }
            }
        }

        if (newPermissions.Count > 0)
        {
            await context.Permissions.AddRangeAsync(newPermissions);
            await context.SaveChangesAsync();
        }

        foreach (MPermission? permission in existingPermissions)
        {
            if (string.IsNullOrWhiteSpace(permission.UiKey)) permission.UiKey = permission.Name;
            permission.Type = PermissionType.Action;
        }

        await context.SaveChangesAsync();
    }
}
