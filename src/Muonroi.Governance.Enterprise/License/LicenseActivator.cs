using Muonroi.Logging.Abstractions;
using System.Net.Http.Json;

namespace Muonroi.Governance.Enterprise.License;

/// <summary>
/// Handles online license activation to generate activation proofs.
///
/// ACTIVATION WORKFLOW:
/// 1. Development/CI-CD (internet available):
///    - Developer runs `dotnet build` or `dotnet run`
///    - LicenseActivator connects to Muonroi server
///    - Server validates license and returns signed activation proof
///    - Proof saved to licenses/activation_proof.json
///
/// 2. Production (offline):
///    - Application loads pre-generated activation_proof.json
///    - Verifies signature offline (no internet needed)
///    - Works in air-gapped environments
///
/// This leverages the natural deployment pipeline where dev/staging has internet access.
/// </summary>
public sealed class LicenseActivator(
    IHttpClientFactory httpClientFactory,
    LicenseConfigs configs,
    IMJsonSerializeService jsonSerializeService,
    IMLog<LicenseActivator>? logger = null)
{
    private readonly string _basePath = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    /// Attempts to activate the license online and save the activation proof.
    /// Returns true if activation succeeded, false otherwise.
    /// </summary>
    public async Task<bool> TryActivateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ActivationProof proof = await ActivateAsync(cancellationToken);
            return proof != null;
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "[License] Activation failed - will retry later or use offline mode");
            return false;
        }
    }

    /// <summary>
    /// Activates the license online and generates an activation proof.
    /// Throws exceptions on failure.
    /// </summary>
    public async Task<ActivationProof> ActivateAsync(CancellationToken cancellationToken = default)
    {
        logger?.LogInformation("[License] Starting online activation...");

        // 1. Read license key
        string? licenseKey = await ReadLicenseKeyAsync();
        if (string.IsNullOrEmpty(licenseKey))
        {
            throw new LicenseException("License key not found. Please configure LicenseFilePath in appsettings.json");
        }

        // 2. Prepare activation request
        ActivationRequest request = new()
        {
            LicenseKey = licenseKey,
            MachineFingerprint = GetMachineFingerprint(),
            ProductVersion = GetProductVersion(),
            ActivationTime = DateTimeOffset.UtcNow,
            Environment = GetEnvironmentName()
        };

        // 3. Send activation request to server
        HttpClient client = httpClientFactory.CreateClient("LicenseServer");
        string activationUrl = configs.Online.Endpoint ?? "https://license.muonroi.com";
        string endpoint = $"{activationUrl.TrimEnd('/')}/api/v1/activate";

        logger?.LogInformation("[License] Connecting to activation server: {Endpoint}", endpoint);

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(endpoint, request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new LicenseException(
                $"Cannot connect to license server at {endpoint}. " +
                "Please ensure internet connectivity during first activation. " +
                $"Error: {ex.Message}", ex);
        }

        // 4. Handle response
        if (!response.IsSuccessStatusCode)
        {
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new LicenseException(
                $"Activation failed: {response.StatusCode}. " +
                $"Error: {errorContent}");
        }

        ActivationResponse? activationResponse = await response.Content.ReadFromJsonAsync<ActivationResponse>(cancellationToken: cancellationToken);

        if (activationResponse == null || !activationResponse.Success || activationResponse.Proof == null)
        {
            throw new LicenseException(
                $"Activation failed: {activationResponse?.Error ?? "Unknown error"}");
        }

        // 5. Save activation proof
        ActivationProof proof = activationResponse.Proof;
        await SaveActivationProofAsync(proof);

        logger?.LogInformation(
            "[License] ✅ Activation successful!\n" +
            "   Organization: {Organization}\n" +
            "   Tier: {Tier}\n" +
            "   Valid until: {ExpiresAt:yyyy-MM-dd}\n" +
            "   Proof saved to: {ProofPath}",
            proof.OrganizationName,
            proof.Tier,
            proof.ExpiresAt,
            GetActivationProofPath());

        if (!string.IsNullOrEmpty(activationResponse.Message))
        {
            logger?.LogInformation("[License] Server message: {Message}", activationResponse.Message);
        }

        return proof;
    }

    /// <summary>
    /// Saves the activation proof to disk.
    /// </summary>
    private async Task SaveActivationProofAsync(ActivationProof proof)
    {
        string proofPath = GetActivationProofPath();
        string? directory = Path.GetDirectoryName(proofPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = jsonSerializeService.Serialize(proof);

        await File.WriteAllTextAsync(proofPath, json);

        logger?.LogDebug("[License] Activation proof saved to: {Path}", proofPath);
    }

    /// <summary>
    /// Reads the license key from configuration.
    /// Supports multiple sources: file, environment variable, or direct config.
    /// </summary>
    private async Task<string?> ReadLicenseKeyAsync()
    {
        // 1. Try reading from file
        if (!string.IsNullOrEmpty(configs.LicenseFilePath))
        {
            string filePath = Path.IsPathRooted(configs.LicenseFilePath)
                ? configs.LicenseFilePath
                : Path.Combine(_basePath, configs.LicenseFilePath);

            if (File.Exists(filePath))
            {
                try
                {
                    string content = await File.ReadAllTextAsync(filePath);

                    // Try parsing as JSON (old format: { "LicenseKey": "..." })
                    if (content.TrimStart().StartsWith("{"))
                    {
                        JsonDocument doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("LicenseKey", out JsonElement keyProp))
                        {
                            return keyProp.GetString();
                        }
                    }

                    // Otherwise, treat entire file as license key
                    return content.Trim();
                }
                catch (Exception ex)
                {
                    logger?.Error(ex, "[License] Failed to read license file: {Path}", filePath);
                }
            }
        }

        // 2. Try environment variable
        string? envKey = Environment.GetEnvironmentVariable("MUONROI_LICENSE_KEY");
        if (!string.IsNullOrEmpty(envKey))
        {
            return envKey;
        }

        // 3. No license key found
        return null;
    }

    /// <summary>
    /// Gets the machine fingerprint for tracking (not for enforcement).
    /// </summary>
    private static string GetMachineFingerprint()
    {
        try
        {
            // Simple fingerprint: machine name + OS version
            string machineInfo = $"{Environment.MachineName}|{Environment.OSVersion}";
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(machineInfo));
            return Convert.ToBase64String(hash);
        }
        catch
        {
            return "UNKNOWN";
        }
    }

    /// <summary>
    /// Gets the product version from assembly.
    /// </summary>
    private static string GetProductVersion()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Version? version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }
        catch
        {
            return "1.0.0";
        }
    }

    /// <summary>
    /// Gets the current environment name.
    /// </summary>
    private static string GetEnvironmentName()
    {
        string? env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        return env ?? "Production";
    }

    /// <summary>
    /// Gets the path where activation proof should be saved.
    /// </summary>
    private string GetActivationProofPath()
    {
        string configuredPath = configs.ActivationProofPath ?? "licenses/activation_proof.json";

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_basePath, configuredPath);
    }
}
