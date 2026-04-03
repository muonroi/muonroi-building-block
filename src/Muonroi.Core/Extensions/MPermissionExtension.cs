namespace Muonroi.Core.Extensions;

/// <summary>
/// Provides extension methods for managing permissions.
/// </summary>
/// <typeparam name="TPermission">The enum type representing permissions.</typeparam>
public static class MPermissionExtension<TPermission> where TPermission : Enum
{
    /// <summary>
    /// Calculates a bitmask from a list of permissions.
    /// </summary>
    /// <param name="userPermissions">The list of permissions to aggregate.</param>
    /// <returns>A bitmask representing the combined permissions.</returns>
    public static long CalculatePermissionsBitmask(List<TPermission> userPermissions)
    {
        return userPermissions.Aggregate<TPermission?, long>(0, (current, permission) => current | Convert.ToInt64(permission));
    }
}
