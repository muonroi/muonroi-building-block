namespace Muonroi.AspNetCore.Attributes;

/// <summary>
/// Specifies that the decorated controller or action requires a specific permission
/// defined by an enum type.
/// </summary>
/// <typeparam name="TPermission">
/// The enum type representing the permission set used by the application.
/// </typeparam>
/// <remarks>
/// This attribute enables strongly-typed permission authorization by using
/// an enum instead of string-based permission keys.
///
/// It improves type safety and reduces the risk of runtime errors caused by
/// incorrect permission names.
///
/// Multiple <see cref="PermissionAttribute{TPermission}"/> attributes can be
/// applied to the same class or method to define multiple permission requirements.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class PermissionAttribute<TPermission> : Attribute where TPermission : Enum
{
    /// <summary>
    /// Gets the required permission that must be granted for the current user
    /// to access the decorated resource.
    /// </summary>
    public TPermission RequiredPermission { get; }

    /// <summary>
    /// Gets the matching mode used when evaluating permission requirements.
    /// </summary>
    /// <remarks>
    /// Defines how the authorization system evaluates the required permissions
    /// when multiple permissions are present.
    /// </remarks>
    public PermissionMatchMode MatchMode { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionAttribute{TPermission}"/> class.
    /// </summary>
    /// <param name="requiredPermission">
    /// The permission value that the current user must possess.
    /// </param>
    /// <param name="matchMode">
    /// The matching mode used to validate the permission requirement.
    /// Default is <see cref="PermissionMatchMode.Any"/>.
    /// </param>
    /// <exception cref="InvalidPermissionException">
    /// Thrown when the specified permission or match mode is invalid.
    /// </exception>
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
