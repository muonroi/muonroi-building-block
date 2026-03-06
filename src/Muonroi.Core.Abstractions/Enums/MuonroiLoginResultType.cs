namespace Muonroi.Core.Abstractions.Enums;

public enum MLoginResultType : byte
{
    Success = 1,
    InvalidUserNameOrEmailAddress,
    InvalidPassword,
    UserIsNotActive,
    InvalidTenancyName,
    TenantIsNotActive,
    UserEmailIsNotConfirmed,
    UnknownExternalLogin,
    LockedOut,
    UserPhoneNumberIsNotConfirmed,
    FailedForOtherReason
}
