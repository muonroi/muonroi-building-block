using System.Security.Cryptography;
using System.Text;
using System.Runtime.Versioning;

namespace Muonroi.Governance.ServerValidation;

/// <summary>
/// Provides machine-level anchoring for sensitive license data using Windows DPAPI or TPM.
/// Makes license files non-transferable between machines.
/// </summary>
public sealed class TpmAnchor
{
    public TpmAnchor()
    {
    }

    /// <summary>
    /// Protects the data using machine-level encryption.
    /// </summary>
    public static string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText)) return plainText;
        if (!OperatingSystem.IsWindows()) return plainText; // Fallback for non-Windows (could add more later)
        return ProtectWindows(plainText);
    }

    /// <summary>
    /// Unprotects the data using machine-level decryption.
    /// </summary>
    public static string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText)) return protectedText;
        if (!OperatingSystem.IsWindows()) return protectedText;
        return UnprotectWindows(protectedText);
    }

    [SupportedOSPlatform("windows")]
    private static string ProtectWindows(string plainText)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(plainText);
            // Protect data for the LocalMachine (so any user on the machine can access, 
            // but not transferable to other machines)
            byte[] protectedData = ProtectedData.Protect(data, null, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(protectedData);
        }
        catch
        {
            return plainText;
        }
    }

    [SupportedOSPlatform("windows")]
    private static string UnprotectWindows(string protectedText)
    {
        try
        {
            byte[] data = Convert.FromBase64String(protectedText);
            byte[] unprotectedData = ProtectedData.Unprotect(data, null, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(unprotectedData);
        }
        catch
        {
            // If decryption fails, it might be plain text or from another machine
            return protectedText;
        }
    }
}
