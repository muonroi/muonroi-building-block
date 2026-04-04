namespace Muonroi.Core.Abstractions.Enums;

/// <summary>
/// Specifies the result types of a login attempt.
/// </summary>
public enum MLoginResultType : byte
{
    /// <summary>
    /// Login was successful.
    /// </summary>
    Success = 1,

    /// <summary>
    /// Invalid username or email address.
    /// </summary>
    InvalidUserNameOrEmailAddress,

    /// <summary>
    /// Invalid password.
    /// </summary>
    InvalidPassword,

    /// <summary>
    /// User is not active.
    /// </summary>
    UserIsNotActive,

    /// <summary>
    /// Invalid tenancy name.
    /// </summary>
    InvalidTenancyName,

    /// <summary>
    /// Tenant is not active.
    /// </summary>
    TenantIsNotActive,

    /// <summary>
    /// User email is not confirmed.
    /// </summary>
    UserEmailIsNotConfirmed,

    /// <summary>
    /// Unknown external login.
    /// </summary>
    UnknownExternalLogin,

    /// <summary>
    /// User is locked out.
    /// </summary>
    LockedOut,

    /// <summary>
    /// User phone number is not confirmed.
    /// </summary>
    UserPhoneNumberIsNotConfirmed,

    /// <summary>
    /// Failed for another reason.
    /// </summary>
    FailedForOtherReason
}
