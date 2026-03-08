

namespace Muonroi.AspNetCore.Attributes;


[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class AuthorizePermissionAttribute : Attribute
{
    public string PermissionKey { get; }
    public PermissionMatchMode MatchMode { get; }

    public AuthorizePermissionAttribute(string permissionKey,
        PermissionMatchMode matchMode = PermissionMatchMode.All)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            throw new InvalidPermissionException("Permission key is invalid");
        }

        if (!Enum.IsDefined(matchMode))
        {
            throw new InvalidPermissionException($"Invalid permission match mode: {matchMode}");
        }

        PermissionKey = permissionKey;
        MatchMode = matchMode;
    }
}
