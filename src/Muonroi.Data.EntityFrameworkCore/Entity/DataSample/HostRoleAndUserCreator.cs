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
    /// Declarative description of a host role to seed and the permission names it must hold.
    /// </summary>
    private sealed record SeedRole(
        string Name,
        string DisplayName,
        string NormalizedName,
        IReadOnlyList<string> Permissions);

    /// <summary>
    /// The complete set of host roles seeded on bootstrap. To add a role, add an entry here —
    /// no new method or query is required.
    /// </summary>
    private static readonly IReadOnlyList<SeedRole> HostRoles =
    [
        new SeedRole(
            StaticRoleAndUserNames.Host.Admin,
            StaticRoleAndUserNames.Host.AdminDisplayName,
            StaticRoleAndUserNames.Host.AdminNormalizedName,
            [StaticPermissionNames.AuthAll])
    ];

    /// <summary>
    /// Creates the host admin role and user, plus default permissions when missing.
    /// </summary>
    public void Create()
    {
        // SEC-01: Fail fast if env-supplied password is insecure, before any other logic.
        ValidateEnvPasswordComplexity();

        // Seeding runs before any user is authenticated. The runtime tenant/creator query
        // filters are fail-closed, so without this scope they would hide already-seeded rows
        // from the existence checks below while the unfiltered unique indexes still reject a
        // re-insert — crashing on every boot after the first. MSeedingScope bypasses those
        // filters for the whole operation, so individual queries need no IgnoreQueryFilters().
        using MSeedingScope _ = new();

        EnsureHostAdminUser();
        EnsureRolesAndPermissions();
        EnsureRolePermissionAssignments();
    }

    private void EnsureHostAdminUser()
    {
        if (context.Users.Any(u => u.UserName == StaticRoleAndUserNames.Host.AdminUserName))
        {
            return;
        }

        string seedPassword = ResolveSeedPassword();

        MUser user = new()
        {
            UserName = StaticRoleAndUserNames.Host.AdminUserName,
            Name = StaticRoleAndUserNames.Host.Admin,
            Surname = StaticRoleAndUserNames.Host.User,
            EmailAddress = StaticRoleAndUserNames.Host.AdminEmail,
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

    private void ValidateEnvPasswordComplexity()
    {
        if (registry?.Has(MCapability.Auth) != true) return;

        string? envPassword = Environment.GetEnvironmentVariable("MUONROI_SEED_ADMIN_PASSWORD");
        if (!string.IsNullOrWhiteSpace(envPassword) && envPassword.Length < 8)
        {
            throw new MConfigurationException(
                "Seed admin password does not meet minimum complexity (8+ chars).",
                "MUONROI_SEED_ADMIN_PASSWORD");
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
            return envPassword;
        }

        // D-03: Generate a cryptographically random password when env var is absent.
        // The password is hashed immediately — the plaintext is never stored.
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    private void EnsureRolesAndPermissions()
    {
        bool changed = false;

        foreach (SeedRole seed in HostRoles)
        {
            if (context.Set<MRole>().Any(r => r.Name == seed.Name))
            {
                continue;
            }

            _ = context.Set<MRole>().Add(new MRole
            {
                Name = seed.Name,
                DisplayName = seed.DisplayName,
                NormalizedName = seed.NormalizedName,
                IsStatic = true,
                IsDefault = true
            });
            changed = true;
        }

        foreach (string permissionName in HostRoles.SelectMany(r => r.Permissions).Distinct())
        {
            if (context.Set<MPermission>().Any(p => p.Name == permissionName))
            {
                continue;
            }

            _ = context.Set<MPermission>().Add(new MPermission
            {
                Name = permissionName,
                IsGranted = true
            });
            changed = true;
        }

        if (changed)
        {
            _ = context.SaveChanges();
        }
    }

    private void EnsureRolePermissionAssignments()
    {
        bool changed = false;

        MUser? adminUser = context.Users
            .FirstOrDefault(u => u.UserName == StaticRoleAndUserNames.Host.AdminUserName);

        foreach (SeedRole seed in HostRoles)
        {
            MRole? role = context.Set<MRole>().FirstOrDefault(r => r.Name == seed.Name);
            if (role is null)
            {
                continue;
            }

            foreach (string permissionName in seed.Permissions)
            {
                MPermission? permission = context.Set<MPermission>()
                    .FirstOrDefault(p => p.Name == permissionName);
                if (permission is null)
                {
                    continue;
                }

                bool alreadyLinked = context.Set<MRolePermission>()
                    .Any(rp => rp.RoleId == role.EntityId && rp.PermissionId == permission.EntityId);
                if (alreadyLinked)
                {
                    continue;
                }

                _ = context.Set<MRolePermission>().Add(new MRolePermission
                {
                    RoleId = role.EntityId,
                    PermissionId = permission.EntityId
                });
                changed = true;
            }

            // The seeded host admin user is granted the Admin role.
            if (adminUser is not null && seed.Name == StaticRoleAndUserNames.Host.Admin)
            {
                bool userAlreadyLinked = context.Set<MUserRole>()
                    .Any(ur => ur.UserId == adminUser.EntityId && ur.RoleId == role.EntityId);
                if (!userAlreadyLinked)
                {
                    _ = context.Set<MUserRole>().Add(new MUserRole
                    {
                        UserId = adminUser.EntityId,
                        RoleId = role.EntityId
                    });
                    changed = true;
                }
            }
        }

        if (changed)
        {
            _ = context.SaveChanges();
        }
    }
}
