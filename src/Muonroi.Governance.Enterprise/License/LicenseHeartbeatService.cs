using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Json;
using System.Text.Json;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.License;

/// <summary>
/// Represents the License Heartbeat Service.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "HttpClient response deserialization values are validated non-null by prior status code checks.")]
public sealed class LicenseHeartbeatService(
    IHttpClientFactory httpClientFactory,
    LicenseConfigs configs,
    LicenseState state,
    LicenseRuntimeStatus runtimeStatus,
    IHostEnvironment? hostEnvironment = null,
    IMLog<LicenseHeartbeatService>? logger = null) : BackgroundService
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Executes the Execute Async operation.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (configs.Mode != LicenseMode.Online ||
            string.IsNullOrWhiteSpace(configs.Online.Endpoint) ||
            !configs.Online.EnableHeartbeat ||
            state.ActivationProof == null)
        {
            logger?.Info("[License] Heartbeat disabled.");
            return;
        }

        runtimeStatus.InitializeFromProof(state.ActivationProof);

        // Load persisted nonce from heartbeat state file (separate from signed proof)
        string? persistedNonce = LoadHeartbeatNonce();
        if (!string.IsNullOrWhiteSpace(persistedNonce))
        {
            runtimeStatus.UpdateHeartbeatSuccess(persistedNonce, DateTimeOffset.UtcNow);
        }

        TimeSpan interval = TimeSpan.FromMinutes(
            configs.Online.HeartbeatIntervalMinutes > 0
                ? configs.Online.HeartbeatIntervalMinutes
                : 240);

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendHeartbeatAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "[License] Heartbeat failed.");
                runtimeStatus.EvaluateGracePeriod(DateTimeOffset.UtcNow);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        ActivationProof proof = state.ActivationProof
            ?? throw new MInternalException("Activation proof is required for heartbeat.");

        string nonce = runtimeStatus.CurrentHeartbeatNonce ?? proof.HeartbeatNonce ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nonce))
        {
            logger?.Warn("[License] Heartbeat skipped because nonce is missing.");
            return;
        }

        HttpClient client = httpClientFactory.CreateClient("LicenseServer");
        LicenseHeartbeatResponse? response = await (await client.PostAsJsonAsync(
            $"{configs.Online.Endpoint!.TrimEnd('/')}/api/v1/heartbeat",
            new LicenseHeartbeatRequest
            {
                LicenseId = proof.LicenseId,
                ProofId = proof.ProofId,
                MachineFingerprint = proof.MachineFingerprint ?? string.Empty,
                Nonce = nonce
            },
            cancellationToken)).Content.ReadFromJsonAsync<LicenseHeartbeatResponse>(cancellationToken: cancellationToken);

        if (response == null || !response.Success)
        {
            string error = response?.Error ?? "unknown heartbeat error";
            logger?.Warn("[License] Heartbeat rejected: {Error}", error);
            runtimeStatus.EvaluateGracePeriod(DateTimeOffset.UtcNow);
            return;
        }

        if (response.IsRevoked)
        {
            DateTimeOffset graceUntil = response.GraceUntilUtc
                ?? DateTimeOffset.UtcNow.AddHours(Math.Max(1, configs.Online.RevocationGraceHours));
            runtimeStatus.StartRevocationGrace(graceUntil);
            runtimeStatus.EvaluateGracePeriod(DateTimeOffset.UtcNow);
            logger?.Warn("[License] License revoked. Grace until {GraceUntil}.", graceUntil);
            return;
        }

        runtimeStatus.UpdateHeartbeatSuccess(response.NewNonce, response.CheckedAtUtc);
        SaveHeartbeatNonce(response.NewNonce);
        logger?.Info("[License] Heartbeat ok at {CheckedAtUtc}.", response.CheckedAtUtc);
    }

    private string GetHeartbeatStatePath()
    {
        string root = !string.IsNullOrWhiteSpace(hostEnvironment?.ContentRootPath)
            ? hostEnvironment.ContentRootPath
            : AppContext.BaseDirectory;
        return Path.Combine(root, "licenses", "heartbeat_state.json");
    }

    private string? LoadHeartbeatNonce()
    {
        try
        {
            string path = GetHeartbeatStatePath();
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("nonce", out JsonElement el) ? el.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private void SaveHeartbeatNonce(string? nonce)
    {
        if (string.IsNullOrWhiteSpace(nonce)) return;
        try
        {
            string path = GetHeartbeatStatePath();
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // MBB002-exempt: heartbeat state is internal runtime data, not domain serialization
            string json = JsonSerializer.Serialize(new { nonce, updatedAt = DateTimeOffset.UtcNow }, _jsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            logger?.Warn("[License] Failed to persist heartbeat nonce: {Message}", ex.Message);
        }
    }
}
