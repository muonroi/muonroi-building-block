namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class MAuthControllerBaseTests
{
    private static TestAuthController CreateController(
        out IAuthService<TestPerm, TestDbContext> auth,
        out IPermissionService<TestPerm> perm)
    {
        auth = Substitute.For<IAuthService<TestPerm, TestDbContext>>();
        perm = Substitute.For<IPermissionService<TestPerm>>();
        return new TestAuthController(auth, perm);
    }

    [Fact]
    public async Task GetPermissionTree_Returns_Tree()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        PermissionTree tree = new();
        MResponse<PermissionTree> resp = new()
        {
            Result = tree
        };
        perm.GetUserPermissionTreeAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetPermissionTree(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
        await perm.Received(1).GetUserPermissionTreeAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPermissionTree_Returns_Null_Tree()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<PermissionTree> resp = new();
        perm.GetUserPermissionTreeAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetPermissionTree(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetPermissionTree_NoPermission()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<PermissionTree> resp = new();
        resp.AddError("NO_PERMISSION");
        perm.GetUserPermissionTreeAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetPermissionTree(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetRolePermissions_Returns_List()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid roleId = Guid.NewGuid();
        MResponse<List<MPermission>> resp = new()
        {
            Result = [new MPermission()]
        };
        perm.GetRolePermissionsAsync(roleId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetRolePermissions(roleId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetRolePermissions_Role_Not_Found()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid roleId = Guid.NewGuid();
        MResponse<List<MPermission>> resp = new();
        resp.AddError("NOT_FOUND");
        perm.GetRolePermissionsAsync(roleId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetRolePermissions(roleId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetRolePermissions_Returns_Empty()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid roleId = Guid.NewGuid();
        MResponse<List<MPermission>> resp = new()
        {
            Result = null
        };
        perm.GetRolePermissionsAsync(roleId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetRolePermissions(roleId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetRoleUsers_Returns_List()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid roleId = Guid.NewGuid();
        MResponse<List<MUser>> resp = new()
        {
            Result = [new MUser()]
        };
        perm.GetRoleUsersAsync(roleId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetRoleUsers(roleId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetRoleUsers_Role_Not_Found()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid roleId = Guid.NewGuid();
        MResponse<List<MUser>> resp = new();
        resp.AddError("NOT_FOUND");
        perm.GetRoleUsersAsync(roleId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetRoleUsers(roleId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetRoleUsers_Returns_Empty()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid roleId = Guid.NewGuid();
        MResponse<List<MUser>> resp = new()
        {
            Result = null
        };
        perm.GetRoleUsersAsync(roleId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetRoleUsers(roleId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetRoles_Returns_List()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<List<MRole>> resp = new()
        {
            Result = [new MRole()]
        };
        perm.GetRolesAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetRoles(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetRoles_Returns_Empty()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<List<MRole>> resp = new()
        {
            Result = null
        };
        perm.GetRolesAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetRoles(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetUserPermissions_Returns_List()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<List<TestPerm>> resp = new()
        {
            Result = [TestPerm.Read]
        };
        perm.GetUserPermissionsAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUserPermissions(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetUserPermissions_User_Not_Found()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<List<TestPerm>> resp = new();
        resp.AddError("NOT_FOUND");
        perm.GetUserPermissionsAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUserPermissions(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetUserPermissions_Returns_Empty()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<List<TestPerm>> resp = new()
        {
            Result = null
        };
        perm.GetUserPermissionsAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUserPermissions(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task Login_Success()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        LoginRequestModel req = new()
        {
            Username = "testuser",
            Password = "testpassword"
        };
        MResponse<LoginResponseModel> resp = new()
        {
            Result = new LoginResponseModel()
        };
        auth.LoginAsync(req, Arg.Any<MTokenInfo>(), Arg.Any<MAuthenticateTokenHelper<TestPerm>>(),
            Arg.Any<IMultiLevelCacheService>(), Arg.Any<CancellationToken>()).Returns(resp);

        MTokenInfo info = new()
        {
            UseRsa = false,
            SymmetricSecretKey = "dummy_signing_key"
        };
        IActionResult result = await controller.Login(req, info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)),
            Substitute.For<IMultiLevelCacheService>(), CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task Login_Returns_Login_Response_With_Tokens()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        LoginRequestModel req = new()
        {
            Username = "testuser",
            Password = "testpassword"
        };
        const string expectedAccess = "access-token";
        const string expectedRefresh = "refresh-token";
        MResponse<LoginResponseModel> resp = new()
        {
            Result = new LoginResponseModel
            {
                AccessToken = expectedAccess,
                RefreshToken = expectedRefresh
            }
        };
        auth.LoginAsync(req, Arg.Any<MTokenInfo>(), Arg.Any<MAuthenticateTokenHelper<TestPerm>>(),
            Arg.Any<IMultiLevelCacheService>(), Arg.Any<CancellationToken>()).Returns(resp);

        MTokenInfo info = new()
        {
            UseRsa = false,
            SymmetricSecretKey = "dummy_signing_key"
        };
        IActionResult result = await controller.Login(req, info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)),
            Substitute.For<IMultiLevelCacheService>(), CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        MResponse<LoginResponseModel> value = Assert.IsType<MResponse<LoginResponseModel>>(obj.Value);
        Assert.Equal(expectedAccess, value.Result!.AccessToken);
        Assert.Equal(expectedRefresh, value.Result.RefreshToken);
    }

    [Fact]
    public async Task Login_Invalid_Info()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        LoginRequestModel req = new()
        {
            Username = "testuser",
            Password = "testpassword"
        };
        MResponse<LoginResponseModel> resp = new();
        resp.AddError("INVALID");
        auth.LoginAsync(req, Arg.Any<MTokenInfo>(), Arg.Any<MAuthenticateTokenHelper<TestPerm>>(),
            Arg.Any<IMultiLevelCacheService>(), Arg.Any<CancellationToken>()).Returns(resp);

        MTokenInfo info = new()
        {
            UseRsa = false,
            SymmetricSecretKey = "dummy_signing_key"
        };
        IActionResult result = await controller.Login(req, info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)),
            Substitute.For<IMultiLevelCacheService>(), CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task Login_Account_Locked()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        LoginRequestModel req = new()
        {
            Username = "testuser",
            Password = "testpassword"
        };
        MResponse<LoginResponseModel> resp = new();
        resp.AddError("LOCKED");
        auth.LoginAsync(req, Arg.Any<MTokenInfo>(), Arg.Any<MAuthenticateTokenHelper<TestPerm>>(),
            Arg.Any<IMultiLevelCacheService>(), Arg.Any<CancellationToken>()).Returns(resp);

        MTokenInfo info = new()
        {
            UseRsa = false,
            SymmetricSecretKey = "dummy_signing_key"
        };
        IActionResult result = await controller.Login(req, info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)),
            Substitute.For<IMultiLevelCacheService>(), CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task Logout_Success()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        MResponse<object> resp = new();
        auth.LogoutAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.Logout(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task Logout_User_Null()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        MResponse<object> resp = new();
        resp.AddError("USER_NULL");
        auth.LogoutAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.Logout(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task Logout_Multiple_Times()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        MResponse<object> resp = new();
        auth.LogoutAsync(Arg.Any<CancellationToken>()).Returns(resp);

        _ = await controller.Logout(CancellationToken.None);
        _ = await controller.Logout(CancellationToken.None);
        await auth.Received(2).LogoutAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogoutAll_Success()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        MResponse<object> resp = new();
        auth.LogoutAllAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.LogoutAll(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task LogoutAll_User_Null()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        MResponse<object> resp = new();
        resp.AddError("USER_NULL");
        auth.LogoutAllAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.LogoutAll(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task LogoutAll_Multiple_Times()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        MResponse<object> resp = new();
        auth.LogoutAllAsync(Arg.Any<CancellationToken>()).Returns(resp);

        _ = await controller.LogoutAll(CancellationToken.None);
        _ = await controller.LogoutAll(CancellationToken.None);
        await auth.Received(2).LogoutAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshToken_Success()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        RefreshTokenRequestModel req = new();
        MResponse<RefreshTokenResponseModel> resp = new()
        {
            Result = new RefreshTokenResponseModel()
        };
        auth.RefreshTokenAsync(req, Arg.Any<MTokenInfo>(), Arg.Any<MAuthenticateTokenHelper<TestPerm>>(),
            Arg.Any<IMultiLevelCacheService>(), Arg.Any<CancellationToken>()).Returns(resp);

        MTokenInfo info = new()
        {
            UseRsa = false,
            SymmetricSecretKey = "dummy_signing_key"
        };
        IActionResult result = await controller.RefreshToken(req, info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)),
            Substitute.For<IMultiLevelCacheService>(), CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task RefreshToken_Expired()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        RefreshTokenRequestModel req = new();
        MResponse<RefreshTokenResponseModel> resp = new();
        resp.AddError("EXPIRED");
        auth.RefreshTokenAsync(req, Arg.Any<MTokenInfo>(), Arg.Any<MAuthenticateTokenHelper<TestPerm>>(),
            Arg.Any<IMultiLevelCacheService>(), Arg.Any<CancellationToken>()).Returns(resp);

        MTokenInfo info = new()
        {
            UseRsa = false,
            SymmetricSecretKey = "dummy_signing_key"
        };
        IActionResult result = await controller.RefreshToken(req, info, new MAuthenticateTokenHelper<TestPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)),
            Substitute.For<IMultiLevelCacheService>(), CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task Register_Success()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        RegisterRequestModel req = new();
        MResponse<LoginResponseModel> resp = new()
        {
            Result = new LoginResponseModel()
        };
        auth.RegisterAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.Register(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task Register_Email_Exists()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        RegisterRequestModel req = new();
        MResponse<LoginResponseModel> resp = new();
        resp.AddError("EXISTS");
        auth.RegisterAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.Register(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task Register_Invalid_Info()
    {
        TestAuthController controller = CreateController(out IAuthService<TestPerm, TestDbContext>? auth, out _);
        RegisterRequestModel req = new();
        MResponse<LoginResponseModel> resp = new();
        resp.AddError("INVALID");
        auth.RegisterAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.Register(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task CreateRole_Returns_Role()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        CreateRoleRequestModel req = new() { Name = "RoleTest", DisplayName = "Role test displayname" };
        MResponse<MRole> resp = new()
        {
            Result = new MRole()
        };
        perm.CreateRoleAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.CreateRole(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task CreateRole_Role_Null()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        CreateRoleRequestModel req = new() { Name = "RoleTest", DisplayName = "Role test displayname" };
        MResponse<MRole> resp = new();
        perm.CreateRoleAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.CreateRole(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task CreateRole_Duplicate()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        CreateRoleRequestModel req = new() { Name = "RoleTest", DisplayName = "Role test displayname" };
        MResponse<MRole> resp = new();
        resp.AddError("DUPLICATE");
        perm.CreateRoleAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.CreateRole(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task AssignPermissionToRole_Success()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        AssignPermissionRequestModel req = new();
        MResponse<object> resp = new();
        perm.AssignPermissionToRoleAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.AssignPermissionToRole(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task AssignPermissionToRole_Invalid()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        AssignPermissionRequestModel req = new();
        MResponse<object> resp = new();
        resp.AddError("INVALID");
        perm.AssignPermissionToRoleAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.AssignPermissionToRole(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task AssignPermissionToRole_DbError()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        AssignPermissionRequestModel req = new();
        MResponse<object> resp = new();
        resp.AddError("DB_ERROR");
        perm.AssignPermissionToRoleAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.AssignPermissionToRole(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task AssignRoleToUser_Success()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        AssignRoleRequestModel req = new();
        MResponse<object> resp = new();
        perm.AssignRoleToUserAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.AssignRoleToUser(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task AssignRoleToUser_Invalid()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        AssignRoleRequestModel req = new();
        MResponse<object> resp = new();
        resp.AddError("INVALID");
        perm.AssignRoleToUserAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.AssignRoleToUser(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task AssignRoleToUser_DbError()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        AssignRoleRequestModel req = new();
        MResponse<object> resp = new();
        resp.AddError("DB_ERROR");
        perm.AssignRoleToUserAsync(req, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.AssignRoleToUser(req, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task DeleteRole_Success()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid id = Guid.NewGuid();
        MResponse<object> resp = new();
        perm.DeleteRoleAsync(id, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.DeleteRole(id, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task DeleteRole_NotFound()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid id = Guid.NewGuid();
        MResponse<object> resp = new();
        resp.AddError("NOT_FOUND");
        perm.DeleteRoleAsync(id, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.DeleteRole(id, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task DeleteRole_DbError()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid id = Guid.NewGuid();
        MResponse<object> resp = new();
        resp.AddError("DB_ERROR");
        perm.DeleteRoleAsync(id, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.DeleteRole(id, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetMenuMetadata_Returns_List()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<List<MenuMetadata>> resp = new()
        {
            Result = [new MenuMetadata()]
        };
        perm.GetMenuMetadataAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetMenuMetadata(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetMenuMetadata_Empty()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<List<MenuMetadata>> resp = new()
        {
            Result = null
        };
        perm.GetMenuMetadataAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetMenuMetadata(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetMenuMetadata_NoPermission()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<List<MenuMetadata>> resp = new();
        resp.AddError("NO_PERMISSION");
        perm.GetMenuMetadataAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetMenuMetadata(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetUiManifest_Returns_Manifest()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<MUiManifest> resp = new()
        {
            Result = new MUiManifest
            {
                UserId = userId
            }
        };
        perm.GetUiManifestAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUiManifest(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetUiManifest_Empty()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<MUiManifest> resp = new()
        {
            Result = null
        };
        perm.GetUiManifestAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUiManifest(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetUiManifest_NoPermission()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<MUiManifest> resp = new();
        resp.AddError("NO_PERMISSION");
        perm.GetUiManifestAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUiManifest(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetCurrentUserUiManifest_Returns_Manifest()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(Substitute.For<ILicenseGuard>())
            .BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim(ClaimConstants.UserIdentifier, userId.ToString())], "test"))
            }
        };

        MResponse<MUiManifest> resp = new()
        {
            Result = new MUiManifest
            {
                UserId = userId
            }
        };
        perm.GetUiManifestAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetCurrentUserUiManifest(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetCurrentUserUiManifest_Missing_UserClaim_Returns_Unauthorized()
    {
        TestAuthController controller = CreateController(out _, out _);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(Substitute.For<ILicenseGuard>())
            .BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        IActionResult result = await controller.GetCurrentUserUiManifest(CancellationToken.None);
        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        MResponse<object> payload = Assert.IsType<MResponse<object>>(unauthorized.Value);
        Assert.False(payload.IsOk);
    }

    [Fact]
    public async Task GetUiEngineManifest_Returns_Manifest()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<MUiEngineManifest> resp = new()
        {
            Result = new MUiEngineManifest
            {
                UserId = userId
            }
        };
        perm.GetUiEngineManifestAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUiEngineManifest(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetUiEngineManifest_Empty()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<MUiEngineManifest> resp = new()
        {
            Result = null
        };
        perm.GetUiEngineManifestAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUiEngineManifest(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetUiEngineManifest_NoPermission()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        MResponse<MUiEngineManifest> resp = new();
        resp.AddError("NO_PERMISSION");
        perm.GetUiEngineManifestAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUiEngineManifest(userId, CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetCurrentUserUiEngineManifest_Returns_Manifest()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        Guid userId = Guid.NewGuid();
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(Substitute.For<ILicenseGuard>())
            .BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
                User = new ClaimsPrincipal(
                    new ClaimsIdentity([new Claim(ClaimConstants.UserIdentifier, userId.ToString())], "test"))
            }
        };

        MResponse<MUiEngineManifest> resp = new()
        {
            Result = new MUiEngineManifest
            {
                UserId = userId
            }
        };
        perm.GetUiEngineManifestAsync(userId, Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetCurrentUserUiEngineManifest(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetCurrentUserUiEngineManifest_Missing_UserClaim_Returns_Unauthorized()
    {
        TestAuthController controller = CreateController(out _, out _);
        ServiceProvider services = new ServiceCollection()
            .AddSingleton(Substitute.For<ILicenseGuard>())
            .BuildServiceProvider();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services,
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        IActionResult result = await controller.GetCurrentUserUiEngineManifest(CancellationToken.None);
        UnauthorizedObjectResult unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        MResponse<object> payload = Assert.IsType<MResponse<object>>(unauthorized.Value);
        Assert.False(payload.IsOk);
    }

    [Fact]
    public async Task GetUiEngineContractInfo_Returns_Contract()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<MUiEngineContractInfo> resp = new()
        {
            Result = new MUiEngineContractInfo
            {
                RuntimeSchemaVersion = MUiEngineManifest.MSchemaVersionV1
            }
        };
        perm.GetUiEngineContractInfoAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUiEngineContractInfo(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetUiEngineSchemaHash_Returns_SchemaHash()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<MUiEngineSchemaVersion> resp = new()
        {
            Result = new MUiEngineSchemaVersion
            {
                SchemaHash = "abc",
                OpenApiHash = "abc",
                Version = MUiEngineManifest.MSchemaVersionV1
            }
        };
        perm.GetUiEngineSchemaVersionAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetUiEngineSchemaHash(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task NotifyUiEngineSchemaChange_Returns_Result()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<object> resp = new()
        {
            Result = new { schemaHash = "abc" }
        };
        perm.NotifyUiEngineSchemaChangeAsync(
                Arg.Any<MUiEngineSchemaChangeNotification>(),
                Arg.Any<CancellationToken>())
            .Returns(resp);

        MUiEngineSchemaChangeNotification notification = new()
        {
            SchemaHash = "abc",
            Source = "test"
        };
        IActionResult result = await controller.NotifyUiEngineSchemaChange(
            notification,
            CancellationToken.None);

        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetPermission_Returns_List()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<List<MPermission>> resp = new()
        {
            Result = [new MPermission()]
        };
        perm.GetPermissionsAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetPermission(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetPermission_NotFound()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<List<MPermission>> resp = new();
        resp.AddError("NOT_FOUND");
        perm.GetPermissionsAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetPermission(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetPermission_NullDb()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<List<MPermission>> resp = new();
        resp.AddError("DB_NULL");
        perm.GetPermissionsAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetPermission(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetPermissionDefinitions_Returns_List()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<List<PermissionDefinition>> resp = new()
        {
            Result = [new PermissionDefinition()]
        };
        perm.GetPermissionDefinitionsAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetPermissionDefinitions(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetPermissionDefinitions_Empty()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<List<PermissionDefinition>> resp = new()
        {
            Result = null
        };
        perm.GetPermissionDefinitionsAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetPermissionDefinitions(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    [Fact]
    public async Task GetPermissionDefinitions_NoPermission()
    {
        TestAuthController controller = CreateController(out _, out IPermissionService<TestPerm>? perm);
        MResponse<List<PermissionDefinition>> resp = new();
        resp.AddError("NO_PERMISSION");
        perm.GetPermissionDefinitionsAsync(Arg.Any<CancellationToken>()).Returns(resp);

        IActionResult result = await controller.GetPermissionDefinitions(CancellationToken.None);
        ObjectResult obj = Assert.IsType<ObjectResult>(result);
        Assert.Same(resp, obj.Value);
    }

    private class TestAuthController(
        IAuthService<TestPerm, TestDbContext> auth,
        IPermissionService<TestPerm> perm) : MAuthControllerBase<TestPerm, TestDbContext>(auth, perm)
    {
    }
}

