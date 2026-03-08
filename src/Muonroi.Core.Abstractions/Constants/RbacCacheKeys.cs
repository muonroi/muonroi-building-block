namespace Muonroi.Core.Abstractions.Constants;

/// <summary>
/// Provides methods for generating cache keys used for RBAC (Role-Based Access Control) related data.
/// </summary>
public static class RbacCacheKeys
{
    /// <summary>
    /// Generates a cache key for user permissions based on their entity identifier.
    /// </summary>
    /// <param name="userId">The unique entity identifier of the user.</param>
    /// <returns>A string representing the cache key.</returns>
    public static string UserPermissionsByEntityId(Guid userId)
    {
        return $"rbac:user_permissions:entity:{userId:D}";
    }

    /// <summary>
    /// Generates a cache key for user permissions based on their numeric identifier.
    /// </summary>
    /// <param name="userId">The numeric identifier of the user.</param>
    /// <returns>A string representing the cache key.</returns>
    public static string UserPermissionsByNumericId(long userId)
    {
        return $"rbac:user_permissions:id:{userId}";
    }

    /// <summary>
    /// Generates a legacy cache key for user permissions based on their entity identifier.
    /// </summary>
    /// <param name="userId">The unique entity identifier of the user.</param>
    /// <returns>A string representing the legacy cache key.</returns>
    public static string LegacyUserPermissions(Guid userId)
    {
        return $"user_permissions:{userId:D}";
    }

    /// <summary>
    /// Generates a legacy cache key for user permissions based on their numeric identifier.
    /// </summary>
    /// <param name="userId">The numeric identifier of the user.</param>
    /// <returns>A string representing the legacy cache key.</returns>
    public static string LegacyUserPermissions(long userId)
    {
        return $"user_permissions:{userId}";
    }
}
