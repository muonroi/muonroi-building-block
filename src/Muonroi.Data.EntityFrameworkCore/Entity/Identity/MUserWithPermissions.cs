namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Extends <see cref="MUser"/> with resolved permissions.
/// </summary>
public class MUserWithPermissions : MUser
{
    /// <summary>Gets or sets the user's permissions.</summary>
    public List<MPermission> Permissions { get; set; } = [];

    /// <summary>Checks whether the user has the required permission.</summary>
    /// <typeparam name="TPermission">The enum type representing permissions.</typeparam>
    /// <param name="requiredPermission">The permission to check.</param>
    /// <returns><c>true</c> when the permission is present; otherwise <c>false</c>.</returns>
    public bool HasPermission<TPermission>(TPermission requiredPermission) where TPermission : Enum
    {
        long requiredPermissionValue = Convert.ToInt64(requiredPermission);

        return Permissions.Any(permission =>
            (Convert.ToInt64(permission.Name) & requiredPermissionValue) == requiredPermissionValue);
    }
}
