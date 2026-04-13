using Muonroi.Governance.Operations;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BuildingBlock.Test;

public class MEnterpriseSloPresetServiceTests
{
    private readonly MEnterpriseSloPresetService _service = new();

    [Fact]
    public void GetPresetNames_ReturnsExpectedPresets()
    {
        IReadOnlyList<string> names = _service.GetPresetNames();
        Assert.Equal(["balanced", "regulated", "strict"], names);
    }

    [Fact]
    public void GetPreset_Strict_ReturnsExpectedThresholds()
    {
        MEnterpriseSloPreset preset = _service.GetPreset("strict");

        Assert.Equal("strict", preset.Name);
        Assert.Equal(5.0, preset.Grpc.MaxP95IncreasePercent);
        Assert.Equal(0.005, preset.Grpc.MaxErrorRate);
        Assert.Equal(500, preset.MessageBus.MaxLag);
        Assert.Equal(0.80, preset.DistributedCache.MinHitRate);
    }

    [Fact]
    public void GetPreset_Unknown_Throws()
    {
        Assert.Throws<MArgumentException>(() => _service.GetPreset("unknown-preset"));
    }
}
