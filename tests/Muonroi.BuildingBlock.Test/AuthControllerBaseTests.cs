namespace Muonroi.BuildingBlock.Test;

public class AuthControllerBaseTests
{
    public enum DummyPerm
    {
        One
    }

    private class DummyAuthController(
        IAuthService<DummyPerm, TestDbContext> auth,
        IPermissionService<DummyPerm> perm) : MAuthControllerBase<DummyPerm, TestDbContext>(auth, perm)
    {
    }

    [Fact]
    public async Task RemovePermissionFromRole_Returns_Response()
    {
        IPermissionService<DummyPerm> perm = Substitute.For<IPermissionService<DummyPerm>>();
        perm.RemovePermissionFromRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new MResponse<object>());
        DummyAuthController ctrl = new(Substitute.For<IAuthService<DummyPerm, TestDbContext>>(), perm);

        IActionResult result = await ctrl.RemovePermissionFromRole(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, obj.StatusCode);
        Assert.True(((MVoidMethodResult)obj.Value!).IsOk);
    }

    [Fact]
    public async Task RemovePermissionFromRole_NotFound_Returns_Error()
    {
        IPermissionService<DummyPerm> perm = Substitute.For<IPermissionService<DummyPerm>>();
        MResponse<object> resp = new();
        resp.AddError("err");
        perm.RemovePermissionFromRoleAsync(Guid.Empty, Guid.Empty, default).ReturnsForAnyArgs(resp);
        DummyAuthController ctrl = new(Substitute.For<IAuthService<DummyPerm, TestDbContext>>(), perm);

        IActionResult result = await ctrl.RemovePermissionFromRole(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.False(((MResponse<object>)obj.Value!).IsOk);
    }

    [Fact]
    public async Task RemovePermissionFromRole_DbError_Throws()
    {
        IPermissionService<DummyPerm> perm = Substitute.For<IPermissionService<DummyPerm>>();
        perm.RemovePermissionFromRoleAsync(Guid.Empty, Guid.Empty, default)
            .ThrowsForAnyArgs(new Exception("db"));
        DummyAuthController ctrl = new(Substitute.For<IAuthService<DummyPerm, TestDbContext>>(), perm);

        await Assert.ThrowsAsync<Exception>(() =>
            ctrl.RemovePermissionFromRole(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateRole_Returns_Result()
    {
        IPermissionService<DummyPerm> perm = Substitute.For<IPermissionService<DummyPerm>>();
        MResponse<MRole> resp = new()
        {
            Result = new MRole
            {
                Name = "r"
            }
        };
        perm.UpdateRoleAsync(Arg.Any<UpdateRoleRequestModel>(), Arg.Any<CancellationToken>()).Returns(resp);
        DummyAuthController ctrl = new(Substitute.For<IAuthService<DummyPerm, TestDbContext>>(), perm);

        IActionResult result = await ctrl.UpdateRole(new UpdateRoleRequestModel
        {
            Name = "TestRole",
            DisplayName = "Test Role Display Name"
        }, CancellationToken.None);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        MResponse<MRole> value = Assert.IsType<MResponse<MRole>>(obj.Value);
        Assert.Same(resp.Result, value.Result);
    }

    [Fact]
    public async Task UpdateRole_NotFound_Returns_Error()
    {
        IPermissionService<DummyPerm> perm = Substitute.For<IPermissionService<DummyPerm>>();
        MResponse<MRole> resp = new();
        resp.AddError("err");
        perm.UpdateRoleAsync(default!, default).ReturnsForAnyArgs(resp);
        DummyAuthController ctrl = new(Substitute.For<IAuthService<DummyPerm, TestDbContext>>(), perm);

        IActionResult result = await ctrl.UpdateRole(new UpdateRoleRequestModel
        {
            Name = "TestRole",
            DisplayName = "Test Role Display Name"
        }, CancellationToken.None);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.False(((MResponse<MRole>)obj.Value!).IsOk);
    }

    [Fact]
    public async Task UpdateRole_DbError_Throws()
    {
        IPermissionService<DummyPerm> perm = Substitute.For<IPermissionService<DummyPerm>>();
        perm.UpdateRoleAsync(default!, default)
            .ThrowsForAnyArgs(new Exception("db"));
        DummyAuthController ctrl = new(Substitute.For<IAuthService<DummyPerm, TestDbContext>>(), perm);

        await Assert.ThrowsAsync<Exception>(() => ctrl.UpdateRole(new UpdateRoleRequestModel
        {
            Name = "TestRole",
            DisplayName = "Test Role Display Name"
        }, CancellationToken.None));
    }
}
