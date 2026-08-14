namespace Muonroi.Governance.License;

internal sealed class DefaultLicenseFingerprintProvider(
    LicenseConfigs configs,
    IHostEnvironment? environment) : ILicenseFingerprintProvider
{
    public string GetFingerprint()
    {
        string appName = environment?.ApplicationName ?? "MUONROI_APP";
        string projectSeed = configs.ProjectSeed ?? "DEFAULT_SEED";

        // ProjectOnly: bind to project identity only (no hardware/OS) so the same license
        // runs on any machine (dev / UAT / prod). MachineAndProject (default) also binds hardware.
        string[] parts = configs.FingerprintScope == LicenseFingerprintScope.ProjectOnly
            ?
            [
                appName,
                projectSeed
            ]
            :
            [
                GetHardwareId(),
                Environment.OSVersion.Platform.ToString(),
                appName,
                projectSeed
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
