using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using System.Runtime.InteropServices;
using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Governance.License;

/// <summary>
/// Represents the Fingerprint Provider.
/// </summary>
public sealed class FingerprintProvider(
    LicenseConfigs configs,
    IHostEnvironment? environment) : ILicenseFingerprintProvider
{
    /// <summary>
    /// Executes the Get Fingerprint operation.
    /// </summary>
    public string GetFingerprint()
    {
        string hardwareId = GetHardwareId();
        string[] parts =
        [
            hardwareId,
            Environment.OSVersion.Platform.ToString(),
            environment?.ApplicationName ?? "MUONROI_APP",
            configs.ProjectSeed ?? "DEFAULT_SEED"
        ];

        string raw = string.Join("|", parts) + "|" + (configs.FingerprintSalt ?? string.Empty);
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private static string GetHardwareId()
    {
        string machine = Environment.MachineName;
        string os = RuntimeInformation.OSDescription;
        string arch = RuntimeInformation.OSArchitecture.ToString();
        string proc = Environment.ProcessorCount.ToString();
        return $"{machine}|{os}|{arch}|{proc}";
    }
}
