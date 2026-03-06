

namespace Muonroi.AspNetCore.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAttribute<TPermission> : Attribute where TPermission : Enum
{
    public TPermission RequiredPermission { get; }
    public PermissionMatchMode MatchMode { get; }

    public PermissionAttribute(TPermission requiredPermission, PermissionMatchMode matchMode = PermissionMatchMode.Any)
    {
        if (!Enum.IsDefined(typeof(TPermission), requiredPermission))
        {
            throw new InvalidPermissionException($"Invalid permission: {requiredPermission}");
        }

        if (Enum.IsDefined(matchMode))
        {
            RequiredPermission = requiredPermission;
            MatchMode = matchMode;
        }
        else
        {
            throw new InvalidPermissionException($"Invalid permission match mode: {matchMode}");
        }
    }
}
