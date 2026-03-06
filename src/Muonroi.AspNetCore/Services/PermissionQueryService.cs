using Muonroi.Data.EntityFrameworkCore.Entity.Identity;

namespace Muonroi.AspNetCore.Services;

public sealed class PermissionQueryService<TDbContext>(TDbContext dbContext)
    where TDbContext : MDbContext
{
    public Task<MRole?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MRole>()
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Name == roleName && !r.IsDeleted, cancellationToken);
    }

    public Task<MRole?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MRole>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EntityId == roleId && !x.IsDeleted, cancellationToken);
    }

    public Task<MPermission?> GetPermissionByIdAsync(Guid permissionId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MPermission>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EntityId == permissionId && !x.IsDeleted, cancellationToken);
    }

    public Task<MUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MUser>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EntityId == userId && !x.IsDeleted, cancellationToken);
    }

    public Task<MRolePermission?> GetRolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MRolePermission>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId && !x.IsDeleted, cancellationToken);
    }

    public Task<MUserRole?> GetUserRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MUserRole>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId && !x.IsDeleted, cancellationToken);
    }

    public Task<List<MRole>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MRole>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public Task<List<MPermission>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MPermission>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    public Task<List<MPermission>> GetPermissionsOfRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return (from role in dbContext.Set<MRole>().AsNoTracking()
                join rolePermission in dbContext.Set<MRolePermission>().AsNoTracking() on role.EntityId equals rolePermission.RoleId
                join permission in dbContext.Set<MPermission>().AsNoTracking() on rolePermission.PermissionId equals permission.EntityId
                where role.EntityId == roleId
                      && !permission.IsDeleted
                      && !rolePermission.IsDeleted
                      && !role.IsDeleted
                select permission).ToListAsync(cancellationToken);
    }

    public Task<List<MUser>> GetUsersOfRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return (from role in dbContext.Set<MRole>().AsNoTracking()
                join userRole in dbContext.Set<MUserRole>().AsNoTracking() on role.EntityId equals userRole.RoleId
                join user in dbContext.Set<MUser>().AsNoTracking() on userRole.UserId equals user.EntityId
                where role.EntityId == roleId
                      && !user.IsDeleted
                      && !role.IsDeleted
                      && !userRole.IsDeleted
                select user).ToListAsync(cancellationToken);
    }

    public Task<List<Guid>> GetGrantedPermissionIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return (from userRole in dbContext.Set<MUserRole>().AsNoTracking()
                join rolePermission in dbContext.Set<MRolePermission>().AsNoTracking() on userRole.RoleId equals rolePermission.RoleId
                where userRole.UserId == userId && !userRole.IsDeleted && !rolePermission.IsDeleted
                select rolePermission.PermissionId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public Task<List<MPermission>> GetAllPermissionsWithGroupsAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MPermission>()
            .AsNoTracking()
            .Include(x => x.PermissionGroup)
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.PermissionGroup != null ? x.PermissionGroup.DisplayName : string.Empty)
            .ThenBy(x => x.Order ?? int.MaxValue)
            .ThenBy(x => x.UiKey)
            .ToListAsync(cancellationToken);
    }

    public Task<List<string>> GetPermissionNamesOfUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return (from user in dbContext.Set<MUser>().AsNoTracking()
                join userRole in dbContext.Set<MUserRole>().AsNoTracking() on user.EntityId equals userRole.UserId
                join role in dbContext.Set<MRole>().AsNoTracking() on userRole.RoleId equals role.EntityId
                join rolePermission in dbContext.Set<MRolePermission>().AsNoTracking() on role.EntityId equals rolePermission.RoleId
                join permission in dbContext.Set<MPermission>().AsNoTracking() on rolePermission.PermissionId equals permission.EntityId
                where user.EntityId == userId
                      && !permission.IsDeleted
                      && !rolePermission.IsDeleted
                      && !role.IsDeleted
                      && !user.IsDeleted
                select permission.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
