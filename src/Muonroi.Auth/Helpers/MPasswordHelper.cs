namespace Muonroi.Auth.Helpers;

/// <summary>
/// Provides helper methods for hashing and verifying passwords.
/// </summary>
public static class MPasswordHelper
{
    /// <summary>
    /// Hashes the provided password and generates a salt.
    /// </summary>
    /// <param name="password">The password to hash.</param>
    /// <param name="salt">The generated salt.</param>
    /// <returns>The hashed password.</returns>
    public static string HashPassword(string password, out string salt)
    {
        salt = BCrypts.GenerateSalt(8);
        return BCrypts.HashPassword(password, salt);
    }

    /// <summary>
    /// Verifies the entered password against the stored hash.
    /// </summary>
    /// <param name="enteredPassword">The password entered by the user.</param>
    /// <param name="storedHash">The stored hash to verify against.</param>
    /// <returns><c>true</c> if the password is correct; otherwise, <c>false</c>.</returns>
    public static bool VerifyPassword(string enteredPassword, string storedHash)
    {
        return BCrypts.Verify(enteredPassword, storedHash);
    }
}
