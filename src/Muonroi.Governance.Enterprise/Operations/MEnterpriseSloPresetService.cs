using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Governance.Operations;

/// <summary>
/// Represents the MEnterprise Slo Preset Service.
/// </summary>
public sealed class MEnterpriseSloPresetService : IMEnterpriseSloPresetService
{
    private static readonly Dictionary<string, MEnterpriseSloPreset> Presets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["balanced"] = new MEnterpriseSloPreset
            {
                Name = "balanced",
                Grpc = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 10.0,
                    MaxErrorRate = 0.01
                },
                MessageBus = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 10.0,
                    MaxErrorRate = 0.01,
                    MaxLag = 1000
                },
                DistributedCache = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 10.0,
                    MaxErrorRate = 0.01,
                    MinHitRate = 0.70
                },
                AuditTrail = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 10.0,
                    MaxErrorRate = 0.01,
                    MaxTamperDetections = 0
                },
                AntiTampering = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 10.0,
                    MaxErrorRate = 0.01,
                    MaxTamperDetections = 0
                }
            },
            ["strict"] = new MEnterpriseSloPreset
            {
                Name = "strict",
                Grpc = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 5.0,
                    MaxErrorRate = 0.005
                },
                MessageBus = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 5.0,
                    MaxErrorRate = 0.005,
                    MaxLag = 500
                },
                DistributedCache = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 5.0,
                    MaxErrorRate = 0.005,
                    MinHitRate = 0.80
                },
                AuditTrail = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 5.0,
                    MaxErrorRate = 0.005,
                    MaxTamperDetections = 0
                },
                AntiTampering = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 5.0,
                    MaxErrorRate = 0.005,
                    MaxTamperDetections = 0
                }
            },
            ["regulated"] = new MEnterpriseSloPreset
            {
                Name = "regulated",
                Grpc = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 3.0,
                    MaxErrorRate = 0.001
                },
                MessageBus = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 3.0,
                    MaxErrorRate = 0.001,
                    MaxLag = 250
                },
                DistributedCache = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 3.0,
                    MaxErrorRate = 0.001,
                    MinHitRate = 0.85
                },
                AuditTrail = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 3.0,
                    MaxErrorRate = 0.001,
                    MaxTamperDetections = 0
                },
                AntiTampering = new MEnterpriseSloModuleThreshold
                {
                    MaxP95IncreasePercent = 3.0,
                    MaxErrorRate = 0.001,
                    MaxTamperDetections = 0
                }
            }
        };

    /// <summary>
    /// Executes the Get Preset Names operation.
    /// </summary>
    public IReadOnlyList<string> GetPresetNames()
    {
        return Presets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>
    /// Executes the Get Preset operation.
    /// </summary>
    public MEnterpriseSloPreset GetPreset(string? presetName)
    {
        string key = string.IsNullOrWhiteSpace(presetName) ? "balanced" : presetName.Trim();
        if (Presets.TryGetValue(key, out MEnterpriseSloPreset? preset))
        {
            return preset;
        }

        MGuard.Against(true, $"Unknown SLO preset '{presetName}'.");
        return default!; // unreachable — MGuard.Against always throws when condition is true
    }
}
