using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

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
