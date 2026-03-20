namespace Muonroi.Governance.Operations;

/// <summary>
/// Represents the IMEnterprise Slo Preset Service.
/// </summary>
public interface IMEnterpriseSloPresetService
{
    /// <summary>
    /// Executes the Get Preset Names operation.
    /// </summary>
    IReadOnlyList<string> GetPresetNames();
    /// <summary>
    /// Executes the Get Preset operation.
    /// </summary>
    MEnterpriseSloPreset GetPreset(string? presetName);
}
