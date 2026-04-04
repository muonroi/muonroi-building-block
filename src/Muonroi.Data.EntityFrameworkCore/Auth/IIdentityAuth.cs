namespace Muonroi.Data.EntityFrameworkCore.Auth;

/// <summary>
/// Exposes identity-related entity sets required by authentication services.
/// </summary>
public interface IIdentityAuth
{
    /// <summary>Gets or sets the users set.</summary>
    DbSet<MUser> Users { get; set; }
    /// <summary>Gets or sets the roles set.</summary>
    DbSet<MRole> Roles { get; set; }
    /// <summary>Gets or sets the permissions set.</summary>
    DbSet<MPermission> Permissions { get; set; }
    /// <summary>Gets or sets the user-role mapping set.</summary>
    DbSet<MUserRole> UserRoles { get; set; }
    /// <summary>Gets or sets the languages set.</summary>
    DbSet<MLanguage> Languages { get; set; }
    /// <summary>Gets or sets the user tokens set.</summary>
    DbSet<MUserToken> UserTokens { get; set; }
    /// <summary>Gets or sets the login attempts set.</summary>
    DbSet<MUserLoginAttempt> MUserLoginAttempts { get; set; }
    /// <summary>Gets or sets the permission groups set.</summary>
    DbSet<MPermissionGroup> PermissionGroups { get; set; }
    /// <summary>Gets or sets the permission audit logs set.</summary>
    DbSet<MPermissionAuditLog> PermissionAuditLogs { get; set; }
}
