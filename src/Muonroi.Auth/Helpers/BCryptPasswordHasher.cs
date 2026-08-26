namespace Muonroi.Auth.Helpers;

/// <summary>
/// BCrypt implementation of <see cref="IPasswordHasher"/>.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <inheritdoc />
    public string HashPassword(string password, out string salt)
    {
        salt = BCrypts.GenerateSalt(PasswordHashingDefaults.BCryptWorkFactor);
        return BCrypts.HashPassword(password, salt);
    }

    /// <inheritdoc />
    public bool VerifyPassword(string enteredPassword, string storedHash)
    {
        return BCrypts.Verify(enteredPassword, storedHash);
    }
}
