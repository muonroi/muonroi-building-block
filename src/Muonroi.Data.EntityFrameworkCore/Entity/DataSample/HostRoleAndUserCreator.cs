using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Exceptions;
using BCrypts = BCrypt.Net.BCrypt;

namespace Muonroi.Data.EntityFrameworkCore.Entity.DataSample;

/// <summary>
/// Seeds the host admin role, user, and default permissions.
/// </summary>
/// <typeparam name="TContext">The EF Core context type.</typeparam>
/// <param name="context">The database context.</param>
/// <param name="dateTimeService">The date/time service.</param>
/// <param name="registry">
/// Optional ecosystem registry. When provided:
/// +Logging — emits a warn log after seeding (password change required).
/// +Auth — enforces minimum password complexity (8+ chars) on env-supplied passwords.
/// </param>
public class HostRoleAndUserCreator<TContext>(
    TContext context,
    IMDateTimeService dateTimeService,
    IMEcosystemRegistry? registry = null)
    where TContext : MDbContext
{
    /// <summary>
    /// Creates the host admin role and user, plus default permissions when missing.
    /// </summary>
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

        string seedPassword = ResolveSeedPassword();

        MUser user = new()
        {
            UserName = StaticRoleAndUserNames.Host.AdminUserName,
            Name = "Admin",
            Surname = "User",
            EmailAddress = "admin@muonroi.com",
            Password = BCrypts.HashPassword(seedPassword),
            IsEmailConfirmed = true,
            ShouldChangePasswordOnNextLogin = true, // Always require password change on first login (D-02)
            IsActive = true,
            CreationTime = dateTimeService.UtcNow()
        };

        _ = context.Users.Add(user);
        _ = context.SaveChanges();

        // +Logging: emit a structured warn log when Logging capability is active (D-05).
        // INTENTIONAL DEGRADATION: Console.WriteLine used because IMLog<T> is not available
        // at seed time — DI container has not been built yet when seeding runs.
        if (registry?.Has(MCapability.Logging) == true)
        {
            Console.WriteLine("[Ecosystem] WARN: Default admin seeded — password change required on next login");
        }
    }

    /// <summary>
    /// Resolves the seed admin password from the environment or generates a random one.
    /// </summary>
    /// <returns>The plaintext password to hash and store.</returns>
    private string ResolveSeedPassword()
    {
        string? envPassword = Environment.GetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD");

        if (!string.IsNullOrWhiteSpace(envPassword))
        {
            // +Auth: enforce minimum password complexity when Auth capability is active (D-06)
            if (registry?.Has(MCapability.Auth) == true && envPassword.Length < 8)
            {
                throw new MConfigurationException(
                    "Seed admin password does not meet minimum complexity (8+ chars).",
                    "MUONROI_SEED_ADMIN_PASSWORD");
            }

            return envPassword;
        }

        // D-03: Generate a cryptographically random password when env var is absent.
        // The password is hashed immediately — the plaintext is never stored.
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
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
