namespace Muonroi.AspNetCore.Attributes;

/// <summary>
/// Specifies that the decorated controller or action requires a specific permission
/// to be granted for the current user.
/// </summary>
/// <remarks>
/// This attribute is used for permission-based authorization in the application.
/// It allows defining required permission keys and the matching strategy used to
/// evaluate them during request execution.
///
/// Multiple <see cref="AuthorizePermissionAttribute"/> attributes can be applied
/// to the same class or method to enforce multiple permission requirements.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class AuthorizePermissionAttribute : Attribute
{
    /// <summary>
    /// Gets the permission key required to access the decorated resource.
    /// </summary>
    /// <remarks>
    /// The permission key should correspond to a permission defined in the system's
    /// authorization configuration or permission store.
    /// </remarks>
    public string PermissionKey { get; }

    /// <summary>
    /// Gets the matching mode used to evaluate the permission requirement.
    /// </summary>
    /// <remarks>
    /// Determines how the permission should be validated when multiple permissions
    /// are involved.
    /// </remarks>
    public PermissionMatchMode MatchMode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizePermissionAttribute"/> class.
    /// </summary>
    /// <param name="permissionKey">
    /// The unique key that identifies the required permission.
    /// </param>
    /// <param name="matchMode">
    /// The permission matching mode used during authorization validation.
    /// Default is <see cref="PermissionMatchMode.All"/>.
    /// </param>
    /// <exception cref="InvalidPermissionException">
    /// Thrown when the permission key is null, empty, or when the match mode is invalid.
    /// </exception>
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
