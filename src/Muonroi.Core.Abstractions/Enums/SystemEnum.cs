namespace Muonroi.Core.Abstractions.Enums;

/// <summary>
/// Defines standard system-level enumeration values, typically used for error codes or status messages.
/// </summary>
public enum SystemEnum
{
    /// <summary>
    /// An unhandled exception occurred while processing the request.
    /// </summary>
    UnhandledException = 0,

    /// <summary>
    /// Username or password is incorrect.
    /// </summary>
    InvalidCredentials = 1,

    /// <summary>
    /// Permission not found for this user.
    /// </summary>
    PermissionNotFound = 2,

    /// <summary>
    /// Role already exists.
    /// </summary>
    RoleAlreadyExists = 3,

    /// <summary>
    /// Role not found.
    /// </summary>
    RoleNotFound = 4,

    /// <summary>
    /// User not found.
    /// </summary>
    UserNotFound = 5,

    /// <summary>
    /// User has no permissions.
    /// </summary>
    UserHasNoPermissions = 6,

    /// <summary>
    /// Account is locked. Try again in {0} minutes.
    /// </summary>
    AccountIsLocked = 7,

    /// <summary>
    /// Invalid login information.
    /// </summary>
    InvalidLoginInfo = 8,

    /// <summary>
    /// User already exists.
    /// </summary>
    UserAlreadyExists = 9,

    /// <summary>
    /// User already has role.
    /// </summary>
    UserAlreadyHasRole = 10,

    /// <summary>
    /// User does not have role.
    /// </summary>
    RoleAlreadyHasPermission = 11,

    /// <summary>
    /// Role does not have permission.
    /// </summary>
    RolePermissionNotFound = 12,

    /// <summary>
    /// Invalid token validity.
    /// </summary>
    InvalidTokenValidity = 13,

    /// <summary>
    /// Password is required.
    /// </summary>
    InvalidPassword = 14,

    /// <summary>
    /// Username is invalid.
    /// </summary>
    InvalidUserName = 15,

    /// <summary>
    /// Password does not meet strength requirements.
    /// </summary>
    InvalidPasswordStrength = 16,

    /// <summary>
    /// Email address is not valid.
    /// </summary>
    InvalidEmailAddress = 17,

    /// <summary>
    /// Email address already exists.
    /// </summary>
    EmailAlreadyExists = 18,

    /// <summary>
    /// Invalid request format or data.
    /// </summary>
    InvalidRequest = 19,

    /// <summary>
    /// Model validation failed.
    /// </summary>
    ValidationFailed = 20
}
