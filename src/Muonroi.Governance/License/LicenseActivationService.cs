using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Muonroi.Governance.Abstractions.Integrity;
using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Governance.License;

/// <summary>
/// Service for activating and refreshing licenses from a remote server.
/// Supports both on-demand activation and periodic background refresh.
/// </summary>
public sealed class LicenseActivationService : ILicenseActivationService
{
    private readonly LicenseConfigs _configs;
    private readonly ILicenseFingerprintProvider _fingerprintProvider;
    private readonly IHostEnvironment? _environment;
    private readonly HttpClient _httpClient;
    private readonly IMJsonSerializeService _jsonSerializeService;
    private readonly IAssemblyHashCollector _hashCollector;

    /// <summary>
    /// Initializes a new instance of LicenseActivationService.
    /// </summary>
    public LicenseActivationService(
        LicenseConfigs configs,
        ILicenseFingerprintProvider fingerprintProvider,
        IMJsonSerializeService jsonSerializeService,
        IAssemblyHashCollector hashCollector,
        IHostEnvironment? environment = null,
        HttpClient? httpClient = null)
    {
        _configs = configs;
        _fingerprintProvider = fingerprintProvider;
        _jsonSerializeService = jsonSerializeService;
        _hashCollector = hashCollector;
        _environment = environment;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(
            _configs.Online.TimeoutSeconds > 0 ? _configs.Online.TimeoutSeconds : 10);
    }

    /// <summary>
    /// Activates the license with the remote server.
    /// On success, saves the license locally for offline use.
    /// </summary>
    public async Task<LicenseActivationResult> ActivateAsync(string licenseKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_configs.Online.Endpoint))
        {
            return LicenseActivationResult.Failed("License server endpoint not configured.");
        }

        try
        {
            string fingerprint = _fingerprintProvider.GetFingerprint();
            ActivationRequest request = new()
            {
                LicenseKey = licenseKey,
                MachineFingerprint = fingerprint,
                ProductVersion = _environment?.ApplicationName ?? "unknown",
                ActivationTime = DateTimeOffset.UtcNow,
                Environment = _environment?.EnvironmentName ?? "Production",
                AssemblyManifest = _hashCollector.Collect()
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"{_configs.Online.Endpoint.TrimEnd('/')}/api/v1/activate",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync(cancellationToken);
                return LicenseActivationResult.Failed($"Server returned {response.StatusCode}: {error}");
            }

            ActivationResponse? activation = await response.Content.ReadFromJsonAsync<ActivationResponse>(cancellationToken: cancellationToken);
            if (activation?.Success != true || activation.Proof?.SignedLicensePayload == null)
            {
                return LicenseActivationResult.Failed(activation?.Error ?? "Server returned invalid activation proof.");
            }

            LicensePayload payload = activation.Proof.SignedLicensePayload;
            await SaveActivationProofAsync(activation.Proof, cancellationToken);
            // Save license locally for offline use
            await SaveLicenseLocallyAsync(payload, cancellationToken);

            return LicenseActivationResult.Success(payload);
        }
        catch (HttpRequestException ex)
        {
            return LicenseActivationResult.Failed($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return LicenseActivationResult.Failed("Request timed out.");
        }
        catch (Exception ex)
        {
            return LicenseActivationResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes the current license from the server.
    /// Used for periodic validation and feature updates.
    /// </summary>
    public async Task<LicenseActivationResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_configs.Online.Endpoint))
        {
            return LicenseActivationResult.Failed("License server endpoint not configured.");
        }

        LicensePayload? currentLicense = LoadCurrentLicense();
        if (currentLicense?.LicenseId == null || currentLicense.LicenseId == "FREE")
        {
            return LicenseActivationResult.Failed("No active license to refresh.");
        }

        try
        {
            string fingerprint = _fingerprintProvider.GetFingerprint();
            LicenseRefreshRequest request = new()
            {
                LicenseId = currentLicense.LicenseId,
                Fingerprint = fingerprint,
                ServerNonce = currentLicense.ServerNonce
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                $"{_configs.Online.Endpoint}/refresh",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // On refresh failure, keep using cached license
                return LicenseActivationResult.Failed($"Refresh failed: {response.StatusCode}");
            }

            LicensePayload? payload = await response.Content.ReadFromJsonAsync<LicensePayload>(cancellationToken: cancellationToken);
            if (payload == null)
            {
                return LicenseActivationResult.Failed("Server returned empty payload.");
            }

            await SaveLicenseLocallyAsync(payload, cancellationToken);
            return LicenseActivationResult.Success(payload);
        }
        catch (Exception ex)
        {
            // On any error during refresh, keep using cached license
            return LicenseActivationResult.Failed($"Refresh error: {ex.Message}");
        }
    }

    /// <summary>
    /// Deactivates the license, removing it from this machine.
    /// </summary>
    public async Task<bool> DeactivateAsync(CancellationToken cancellationToken = default)
    {
        LicensePayload? currentLicense = LoadCurrentLicense();
        if (currentLicense?.LicenseId == null || currentLicense.LicenseId == "FREE")
        {
            return true; // Nothing to deactivate
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(_configs.Online.Endpoint))
            {
                var request = new { currentLicense.LicenseId, Fingerprint = _fingerprintProvider.GetFingerprint() };
                await _httpClient.PostAsJsonAsync($"{_configs.Online.Endpoint}/deactivate", request, cancellationToken);
            }
        }
        catch
        {
            // Best effort - continue with local deactivation
        }

        // Delete local license file
        string? path = ResolveLicensePath();
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
        }

        return true;
    }

    private LicensePayload? LoadCurrentLicense()
    {
        string? path = ResolveLicensePath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return _jsonSerializeService.Deserialize<LicensePayload>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveLicenseLocallyAsync(LicensePayload payload, CancellationToken cancellationToken)
    {
        string? path = ResolveLicensePath();
        if (string.IsNullOrWhiteSpace(path)) return;

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = _jsonSerializeService.Serialize(payload);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private async Task SaveActivationProofAsync(ActivationProof proof, CancellationToken cancellationToken)
    {
        string? path = ResolveActivationProofPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = _jsonSerializeService.Serialize(proof);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private string? ResolveLicensePath()
    {
        string? path = _configs.LicenseFilePath;
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Path.IsPathRooted(path)) return path;
        string root = !string.IsNullOrWhiteSpace(_environment?.ContentRootPath)
            ? _environment.ContentRootPath
            : AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, path));
    }

    private string? ResolveActivationProofPath()
    {
        string? path = _configs.ActivationProofPath;
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Path.IsPathRooted(path)) return path;
        string root = !string.IsNullOrWhiteSpace(_environment?.ContentRootPath)
            ? _environment.ContentRootPath
            : AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, path));
    }
}

/// <summary>
/// Represents the ILicense Activation Service.
/// </summary>
public interface ILicenseActivationService
{
    /// <summary>
    /// Executes the Activate Async operation.
    /// </summary>
    Task<LicenseActivationResult> ActivateAsync(string licenseKey, CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the Refresh Async operation.
    /// </summary>
    Task<LicenseActivationResult> RefreshAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Executes the Deactivate Async operation.
    /// </summary>
    Task<bool> DeactivateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the License Activation Result.
/// </summary>
public sealed class LicenseActivationResult
{
    /// <summary>
    /// Gets or sets the Is Success.
    /// </summary>
    public bool IsSuccess { get; init; }
    /// <summary>
    /// Gets or sets the Error.
    /// </summary>
    public string? Error { get; init; }
    /// <summary>
    /// Gets or sets the Payload.
    /// </summary>
    public LicensePayload? Payload { get; init; }

    /// <summary>
    /// Executes the Success operation.
    /// </summary>
    public static LicenseActivationResult Success(LicensePayload payload) => new()
    {
        IsSuccess = true,
        Payload = payload
    };

    /// <summary>
    /// Executes the Failed operation.
    /// </summary>
    public static LicenseActivationResult Failed(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };
}

/// <summary>
/// Represents the License Activation Request.
/// </summary>
public sealed class LicenseActivationRequest
{
    /// <summary>
    /// Gets or sets the License Key.
    /// </summary>
    public string? LicenseKey { get; set; }
    /// <summary>
    /// Gets or sets the Fingerprint.
    /// </summary>
    public string? Fingerprint { get; set; }
    /// <summary>
    /// Gets or sets the Project Seed.
    /// </summary>
    public string? ProjectSeed { get; set; }
    /// <summary>
    /// Gets or sets the Machine Name.
    /// </summary>
    public string? MachineName { get; set; }
    /// <summary>
    /// Gets or sets the Application Name.
    /// </summary>
    public string? ApplicationName { get; set; }
}

/// <summary>
/// Represents the License Refresh Request.
/// </summary>
public sealed class LicenseRefreshRequest
{
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string? LicenseId { get; set; }
    /// <summary>
    /// Gets or sets the Fingerprint.
    /// </summary>
    public string? Fingerprint { get; set; }
    /// <summary>
    /// Gets or sets the Server Nonce.
    /// </summary>
    public string? ServerNonce { get; set; }
}
