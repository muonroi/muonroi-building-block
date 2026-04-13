namespace Muonroi.BuildingBlock.Test;

public class MPermissionExtensionTests
{
    [Fact]
    public void CalculatePermissionsBitmask_NoPermissions_ReturnsZero()
    {
        long result = MPermissionExtension<SamplePermission>.CalculatePermissionsBitmask([]);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculatePermissionsBitmask_CombineDistinctPermissions()
    {
        List<SamplePermission> permissions = [SamplePermission.Read, SamplePermission.Write, SamplePermission.Delete];

        long result = MPermissionExtension<SamplePermission>.CalculatePermissionsBitmask(permissions);

        long expected = (long)SamplePermission.Read | (long)SamplePermission.Write | (long)SamplePermission.Delete;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculatePermissionsBitmask_IgnoresDuplicates()
    {
        List<SamplePermission> permissions =
        [
            SamplePermission.Read,
            SamplePermission.Read,
            SamplePermission.Write,
            SamplePermission.Admin
        ];

        long result = MPermissionExtension<SamplePermission>.CalculatePermissionsBitmask(permissions);

        long expected = (long)SamplePermission.Read | (long)SamplePermission.Write | (long)SamplePermission.Admin;
        Assert.Equal(expected, result);
    }

    private enum SamplePermission : long
    {
        Read = 1L << 0,
        Write = 1L << 1,
        Delete = 1L << 2,
        Admin = 1L << 60
    }
}
