namespace Muonroi.Core.Tests;

public class MPermissionExtensionTests
{
    private enum SamplePermission : long
    {
        Read = 1L << 0,
        Write = 1L << 1,
        Delete = 1L << 2,
        Admin = 1L << 60
    }

    [Fact]
    public void CalculatePermissionsBitmask_NoPermissions_ReturnsZero()
    {
        long result = MPermissionExtension<SamplePermission>.CalculatePermissionsBitmask([]);

        result.Should().Be(0);
    }

    [Fact]
    public void CalculatePermissionsBitmask_CombineDistinctPermissions()
    {
        List<SamplePermission> permissions = [SamplePermission.Read, SamplePermission.Write, SamplePermission.Delete];

        long result = MPermissionExtension<SamplePermission>.CalculatePermissionsBitmask(permissions);
        long expected = (long)SamplePermission.Read | (long)SamplePermission.Write | (long)SamplePermission.Delete;

        result.Should().Be(expected);
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

        result.Should().Be(expected);
    }
}
