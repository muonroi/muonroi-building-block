namespace Muonroi.Core.Abstractions.Enums;

/// <summary>
/// Specifies how permissions should be matched.
/// </summary>
public enum PermissionMatchMode
{
    /// <summary>
    /// Any of the specified permissions will satisfy the requirement.
    /// </summary>
    Any = 0,

    /// <summary>
    /// All of the specified permissions are required.
    /// </summary>
    All = 1
}
