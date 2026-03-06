namespace Muonroi.Core.Extensions;

public static class MPermissionExtension<TPermission> where TPermission : Enum
{
    public static long CalculatePermissionsBitmask(List<TPermission> userPermissions)
    {
        return userPermissions.Aggregate<TPermission?, long>(0, (current, permission) => current | Convert.ToInt64(permission));
    }
}
