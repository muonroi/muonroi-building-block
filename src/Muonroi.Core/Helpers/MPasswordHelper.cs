namespace Muonroi.Core.Helpers;

public static class MPasswordHelper
{
    public static string HashPassword(string password, out string salt)
    {
        salt = BCrypts.GenerateSalt(8);
        return BCrypts.HashPassword(password, salt);
    }

    public static bool VerifyPassword(string enteredPassword, string storedHash)
    {
        return BCrypts.Verify(enteredPassword, storedHash);
    }
}
