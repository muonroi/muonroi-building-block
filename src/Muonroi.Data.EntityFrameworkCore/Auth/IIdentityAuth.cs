namespace Muonroi.Data.EntityFrameworkCore.Auth;

public interface IIdentityAuth
{
    DbSet<MUser> Users { get; set; }
    DbSet<MRole> Roles { get; set; }
    DbSet<MPermission> Permissions { get; set; }
    DbSet<MUserRole> UserRoles { get; set; }
    DbSet<MLanguage> Languages { get; set; }
    DbSet<MUserToken> UserTokens { get; set; }
    DbSet<MUserLoginAttempt> MUserLoginAttempts { get; set; }
    DbSet<MPermissionGroup> PermissionGroups { get; set; }
    DbSet<MPermissionAuditLog> PermissionAuditLogs { get; set; }
}
