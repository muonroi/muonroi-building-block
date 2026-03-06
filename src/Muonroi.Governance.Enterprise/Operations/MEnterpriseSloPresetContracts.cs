namespace Muonroi.Governance.Operations;

public sealed class MEnterpriseSloModuleThreshold
{
    public double MaxP95IncreasePercent { get; init; } = 10.0;
    public double MaxErrorRate { get; init; } = 0.01;
    public double? MinHitRate { get; init; }
    public double? MaxLag { get; init; }
    public double? MaxTamperDetections { get; init; }
}

public sealed class MEnterpriseSloPreset
{
    public string Name { get; init; } = string.Empty;
    public MEnterpriseSloModuleThreshold Grpc { get; init; } = new();
    public MEnterpriseSloModuleThreshold MessageBus { get; init; } = new();
    public MEnterpriseSloModuleThreshold DistributedCache { get; init; } = new();
    public MEnterpriseSloModuleThreshold AuditTrail { get; init; } = new();
    public MEnterpriseSloModuleThreshold AntiTampering { get; init; } = new();
}
