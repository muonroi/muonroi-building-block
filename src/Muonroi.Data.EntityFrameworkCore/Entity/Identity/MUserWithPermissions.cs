namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

public class MUserWithPermissions : MUser
{
    public List<MPermission> Permissions { get; set; } = [];

    public bool HasPermission<TPermission>(TPermission requiredPermission) where TPermission : Enum
    {
        long requiredPermissionValue = Convert.ToInt64(requiredPermission);

        return Permissions.Any(permission =>
            (Convert.ToInt64(permission.Name) & requiredPermissionValue) == requiredPermissionValue);
    }
}
