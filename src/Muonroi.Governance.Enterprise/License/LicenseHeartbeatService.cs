using System.Net.Http.Json;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Governance.License;

public sealed class LicenseHeartbeatService(
    IHttpClientFactory httpClientFactory,
    LicenseConfigs configs,
    LicenseState state,
    LicenseRuntimeStatus runtimeStatus,
    IMLog<LicenseHeartbeatService>? logger = null) : BackgroundService
{
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
            ?? throw new InvalidOperationException("Activation proof is required for heartbeat.");

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
        logger?.Info("[License] Heartbeat ok at {CheckedAtUtc}.", response.CheckedAtUtc);
    }
}
