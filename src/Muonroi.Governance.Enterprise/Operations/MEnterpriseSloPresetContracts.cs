namespace Muonroi.Governance.Operations;

/// <summary>
/// Represents the MEnterprise Slo Module Threshold.
/// </summary>
public sealed class MEnterpriseSloModuleThreshold
{
    /// <summary>
    /// Gets or sets the Max P95 Increase Percent.
    /// </summary>
    public double MaxP95IncreasePercent { get; init; } = 10.0;
    /// <summary>
    /// Gets or sets the Max Error Rate.
    /// </summary>
    public double MaxErrorRate { get; init; } = 0.01;
    /// <summary>
    /// Gets or sets the Min Hit Rate.
    /// </summary>
    public double? MinHitRate { get; init; }
    /// <summary>
    /// Gets or sets the Max Lag.
    /// </summary>
    public double? MaxLag { get; init; }
    /// <summary>
    /// Gets or sets the Max Tamper Detections.
    /// </summary>
    public double? MaxTamperDetections { get; init; }
}

/// <summary>
/// Represents the MEnterprise Slo Preset.
/// </summary>
public sealed class MEnterpriseSloPreset
{
    /// <summary>
    /// Gets or sets the Name.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Executes the Grpc operation.
    /// </summary>
    public MEnterpriseSloModuleThreshold Grpc { get; init; } = new();
    /// <summary>
    /// Executes the Message Bus operation.
    /// </summary>
    public MEnterpriseSloModuleThreshold MessageBus { get; init; } = new();
    /// <summary>
    /// Executes the Distributed Cache operation.
    /// </summary>
    public MEnterpriseSloModuleThreshold DistributedCache { get; init; } = new();
    /// <summary>
    /// Executes the Audit Trail operation.
    /// </summary>
    public MEnterpriseSloModuleThreshold AuditTrail { get; init; } = new();
    /// <summary>
    /// Executes the Anti Tampering operation.
    /// </summary>
    public MEnterpriseSloModuleThreshold AntiTampering { get; init; } = new();
}
