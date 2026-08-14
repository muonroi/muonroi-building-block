namespace Muonroi.AspNetCore.Services;

/// <summary>
/// Provides query operations for permissions, roles, and users.
/// </summary>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public sealed class PermissionQueryService<TDbContext>(TDbContext dbContext)
    where TDbContext : MDbContext
{
    /// <summary>
    /// Gets a role by its name asynchronously.
    /// </summary>
    /// <param name="roleName">The name of the role.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The role if found; otherwise, null.</returns>
    public Task<MRole?> GetRoleByNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MRole>()
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Name == roleName && !r.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets a role by its unique identifier asynchronously.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The role if found; otherwise, null.</returns>
    public Task<MRole?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MRole>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EntityId == roleId && !x.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets a permission by its unique identifier asynchronously.
    /// </summary>
    /// <param name="permissionId">The unique identifier of the permission.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The permission if found; otherwise, null.</returns>
    public Task<MPermission?> GetPermissionByIdAsync(Guid permissionId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MPermission>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EntityId == permissionId && !x.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets a user by their unique identifier asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user if found; otherwise, null.</returns>
    public Task<MUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MUser>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EntityId == userId && !x.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets a role-permission mapping asynchronously.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="permissionId">The permission identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mapping if found; otherwise, null.</returns>
    public Task<MRolePermission?> GetRolePermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MRolePermission>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId && !x.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets a user-role mapping asynchronously.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The mapping if found; otherwise, null.</returns>
    public Task<MUserRole?> GetUserRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MUserRole>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.RoleId == roleId && !x.IsDeleted, cancellationToken);
    }

    /// <summary>
    /// Gets all roles asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of roles.</returns>
    public Task<List<MRole>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MRole>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all permissions asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of permissions.</returns>
    public Task<List<MPermission>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.Set<MPermission>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets permissions associated with a specific role asynchronously.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of permissions.</returns>
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

    /// <summary>
    /// Gets users associated with a specific role asynchronously.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of users.</returns>
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

    /// <summary>
    /// Gets granted permission identifiers for a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of permission identifiers.</returns>
    public Task<List<Guid>> GetGrantedPermissionIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return (from userRole in dbContext.Set<MUserRole>().AsNoTracking()
                join rolePermission in dbContext.Set<MRolePermission>().AsNoTracking() on userRole.RoleId equals rolePermission.RoleId
                where userRole.UserId == userId && !userRole.IsDeleted && !rolePermission.IsDeleted
                select rolePermission.PermissionId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all permissions along with their groups asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of permissions.</returns>
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

    /// <summary>
    /// Gets the names of permissions granted to a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of permission names.</returns>
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
