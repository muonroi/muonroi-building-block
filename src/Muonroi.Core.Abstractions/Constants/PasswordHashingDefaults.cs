namespace Muonroi.Core.Abstractions.Constants;

/// <summary>
/// Shared defaults for password hashing so every <see cref="Interfaces.IPasswordHasher"/>
/// implementation and seed path uses the same BCrypt cost.
/// </summary>
public static class PasswordHashingDefaults
{
    /// <summary>
    /// BCrypt work factor (log2 rounds) used for all password hashing in the framework.
    /// 12 meets the OWASP-recommended minimum of 10 with margin for hardware growth,
    /// while staying fast enough for interactive login and seed-time hashing.
    /// </summary>
    public const int BCryptWorkFactor = 12;
}
