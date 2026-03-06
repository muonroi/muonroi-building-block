namespace Muonroi.Governance.Operations;

public interface IMEnterpriseSloPresetService
{
    IReadOnlyList<string> GetPresetNames();
    MEnterpriseSloPreset GetPreset(string? presetName);
}
