


namespace Muonroi.BuildingBlock.Test;

public class MPermissionTests
{
    [Fact]
    public void IsGranted_Get_Returns_Value()
    {
        MPermission perm = new()
        {
            IsGranted = true
        };
        Assert.True(perm.IsGranted);
        perm = new();
        Assert.False(perm.IsGranted);
    }
}
