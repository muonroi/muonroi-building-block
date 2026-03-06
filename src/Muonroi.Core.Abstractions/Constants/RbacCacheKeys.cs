namespace Muonroi.Core.Abstractions.Constants;

public static class RbacCacheKeys
{
    public static string UserPermissionsByEntityId(Guid userId)
    {
        return $"rbac:user_permissions:entity:{userId:D}";
    }

    public static string UserPermissionsByNumericId(long userId)
    {
        return $"rbac:user_permissions:id:{userId}";
    }

    public static string LegacyUserPermissions(Guid userId)
    {
        return $"user_permissions:{userId:D}";
    }

    public static string LegacyUserPermissions(long userId)
    {
        return $"user_permissions:{userId}";
    }
}
