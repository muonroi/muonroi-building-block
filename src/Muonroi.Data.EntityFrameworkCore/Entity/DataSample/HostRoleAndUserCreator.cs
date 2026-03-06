namespace Muonroi.Data.EntityFrameworkCore.Entity.DataSample;

public class HostRoleAndUserCreator<TContext>(TContext context, IMDateTimeService dateTimeService) where TContext : MDbContext
{
    public void Create()
    {
        List<string> permissionName =
        [
            "Auth_All"
        ];
        if (!context.Users.Any(u => u.UserName == StaticRoleAndUserNames.Host.AdminUserName)) CreateHostRoleAndUsers();

        if (!context.Set<MRole>().Any(r => r.Name == "Admin")) CreateDefaultRolesAndPermissions();

        if (!context.Set<MRolePermission>().Any()) AssignPermissionsToRoles(permissionName);
    }

    private void CreateHostRoleAndUsers()
    {
        MUser? adminUserForHost = context.Users.IgnoreQueryFilters()
            .FirstOrDefault(u => u.UserName == StaticRoleAndUserNames.Host.AdminUserName);
        if (adminUserForHost is not null) return;
        MUser user = new()
        {
            UserName = StaticRoleAndUserNames.Host.AdminUserName,
            Name = "Admin",
            Surname = "User",
            EmailAddress = "admin@muonroi.com",
            Password = "$2b$08$xjH/bTjHs/EnYJTHDTFAoOTsrxrz2/6WP4Yrz6JZ9uLvbyyiJKbB6", //sysadmin,
            IsEmailConfirmed = true,
            ShouldChangePasswordOnNextLogin = false,
            IsActive = true,
            CreationTime = dateTimeService.UtcNow()
        };

        _ = context.Users.Add(user);
        _ = context.SaveChanges();
    }

    private void CreateDefaultRolesAndPermissions()
    {
        MRole adminRole = new()
        {
            Name = "Admin",
            DisplayName = "Administrator",
            NormalizedName = "ADMIN",
            IsStatic = true,
            IsDefault = true
        };

        if (context.Set<MRole>().Any(r => r.Name == adminRole.Name)) return;

        _ = context.Set<MRole>().Add(adminRole);
        _ = context.SaveChanges();

        MPermission mPermission = new()
        {
            Name = "Auth_All",
            IsGranted = true
        };
        List<MPermission> permissions =
        [
            mPermission
        ];

        foreach (MPermission? permission in permissions.Where(permission =>
                     !context.Set<MPermission>().Any(p => p.Name == permission.Name)))
            _ = context.Set<MPermission>().Add(permission);

        _ = context.SaveChanges();
    }


    private void AssignPermissionsToRoles(IEnumerable<string> permissionNames)
    {
        MRole? adminRole = context.Set<MRole>().FirstOrDefault(r => r.Name == "Admin");

        List<MPermission> permissions = [.. context.Set<MPermission>().Where(p => permissionNames.Contains(p.Name))];

        if (adminRole != null)
        {
            foreach (MPermission permission in permissions)
                _ = context.Set<MRolePermission>().Add(new MRolePermission
                {
                    RoleId = adminRole.EntityId,
                    PermissionId = permission.EntityId
                });

            MUser? adminUser = context.Users.IgnoreQueryFilters()
                .FirstOrDefault(u => u.UserName == StaticRoleAndUserNames.Host.AdminUserName);
            if (adminUser != null)
            {
                bool existingUserRole = context.Set<MUserRole>()
                    .Any(ur => ur.UserId == adminUser.EntityId && ur.RoleId == adminRole.EntityId);
                if (!existingUserRole)
                    _ = context.Set<MUserRole>().Add(new MUserRole
                    {
                        UserId = adminUser.EntityId,
                        RoleId = adminRole.EntityId
                    });
            }
        }

        _ = context.SaveChanges();
    }
}
