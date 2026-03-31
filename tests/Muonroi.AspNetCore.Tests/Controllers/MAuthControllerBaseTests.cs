using Microsoft.AspNetCore.Mvc;
using Muonroi.AspNetCore.Controllers;
using Muonroi.AspNetCore.Services;
using Muonroi.AspNetCore.Tests.Helpers;
using Muonroi.Core.Abstractions.Models.Common.Requests.Login;
using Muonroi.Core.Abstractions.Models.Common.Requests.Registers;
using Muonroi.Core.Abstractions.Models.Common.Responses.Login;
using Muonroi.Core.Abstractions.Response;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Helpers;
using Muonroi.Caching.Memory.MultiLevel;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using NSubstitute;
using Xunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Quota.Abstractions;

namespace Muonroi.AspNetCore.Tests.Controllers;

public class TestAuthController(
    IAuthService<TestPerm, TestDbContext> authService,
    IPermissionService<TestPerm> permissionService)
    : MAuthControllerBase<TestPerm, TestDbContext>(authService, permissionService)
{
}

public class MAuthControllerBaseTests
{
    private readonly IAuthService<TestPerm, TestDbContext> _authService;
    private readonly IPermissionService<TestPerm> _permissionService;
    private readonly ILicenseGuard _licenseGuard;
    private readonly IServiceProvider _serviceProvider;
    private readonly TestAuthController _controller;

    public MAuthControllerBaseTests()
    {
        _authService = Substitute.For<IAuthService<TestPerm, TestDbContext>>();
        _permissionService = Substitute.For<IPermissionService<TestPerm>>();
        _licenseGuard = Substitute.For<ILicenseGuard>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _serviceProvider.GetService(typeof(ILicenseGuard)).Returns(_licenseGuard);

        _controller = new TestAuthController(_authService, _permissionService);
        
        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = _serviceProvider;
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task CreateRole_ReturnsActionResult()
    {
        var request = new CreateRoleRequestModel { Name = "admin", DisplayName = "Admin" };
        _permissionService.CreateRoleAsync(request, Arg.Any<CancellationToken>()).Returns(new MResponse<MRole> { Result = new MRole() });
        var result = await _controller.CreateRole(request, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task AssignPermissionToRole_ReturnsActionResult()
    {
        var request = new AssignPermissionRequestModel();
        _permissionService.AssignPermissionToRoleAsync(request, Arg.Any<CancellationToken>()).Returns(new MResponse<object>());
        var result = await _controller.AssignPermissionToRole(request, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RemovePermissionFromRole_ReturnsActionResult()
    {
        _permissionService.RemovePermissionFromRoleAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<object>());
        var result = await _controller.RemovePermissionFromRole(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task AssignRoleToUser_ReturnsActionResult()
    {
        var request = new AssignRoleRequestModel();
        _permissionService.AssignRoleToUserAsync(request, Arg.Any<CancellationToken>()).Returns(new MResponse<object>());
        var result = await _controller.AssignRoleToUser(request, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetUserPermissions_ReturnsActionResult()
    {
        _permissionService.GetUserPermissionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<List<TestPerm>>());
        var result = await _controller.GetUserPermissions(Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateRole_ReturnsActionResult()
    {
        var request = new UpdateRoleRequestModel { Name = "admin", DisplayName = "Admin" };
        _permissionService.UpdateRoleAsync(request, Arg.Any<CancellationToken>()).Returns(new MResponse<MRole>());
        var result = await _controller.UpdateRole(request, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeleteRole_ReturnsActionResult()
    {
        _permissionService.DeleteRoleAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<object>());
        var result = await _controller.DeleteRole(Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetRoles_ReturnsActionResult()
    {
        _permissionService.GetRolesAsync(Arg.Any<CancellationToken>()).Returns(new MResponse<List<MRole>>());
        var result = await _controller.GetRoles(CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetPermission_ReturnsActionResult()
    {
        _permissionService.GetPermissionsAsync(Arg.Any<CancellationToken>()).Returns(new MResponse<List<MPermission>>());
        var result = await _controller.GetPermission(CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetRolePermissions_ReturnsActionResult()
    {
        _permissionService.GetRolePermissionsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<List<MPermission>>());
        var result = await _controller.GetRolePermissions(Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetRoleUsers_ReturnsActionResult()
    {
        _permissionService.GetRoleUsersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<List<MUser>>());
        var result = await _controller.GetRoleUsers(Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetPermissionDefinitions_ReturnsActionResult()
    {
        _permissionService.GetPermissionDefinitionsAsync(Arg.Any<CancellationToken>()).Returns(new MResponse<List<PermissionDefinition>>());
        var result = await _controller.GetPermissionDefinitions(CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Logout_ReturnsActionResult()
    {
        _authService.LogoutAsync(Arg.Any<CancellationToken>()).Returns(new MResponse<object>());
        var result = await _controller.Logout(CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task LogoutAll_ReturnsActionResult()
    {
        _authService.LogoutAllAsync(Arg.Any<CancellationToken>()).Returns(new MResponse<object>());
        var result = await _controller.LogoutAll(CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Login_ReturnsActionResult()
    {
        var request = new LoginRequestModel { Username = "u", Password = "p" };
        _authService.LoginAsync(Arg.Any<LoginRequestModel>(), Arg.Any<MTokenInfo>(), Arg.Any<MAuthenticateTokenHelper<TestPerm>>(), Arg.Any<IMultiLevelCacheService>(), Arg.Any<CancellationToken>())
            .Returns(new MResponse<LoginResponseModel>());
        var result = await _controller.Login(request, new MTokenInfo(), Substitute.For<MAuthenticateTokenHelper<TestPerm>>(new MTokenInfo(), Substitute.For<ITokenSigner>(), Substitute.For<IMDateTimeService>(), null), Substitute.For<IMultiLevelCacheService>(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RefreshToken_ReturnsActionResult()
    {
        var request = new RefreshTokenRequestModel();
        _authService.RefreshTokenAsync(Arg.Any<RefreshTokenRequestModel>(), Arg.Any<MTokenInfo>(), Arg.Any<MAuthenticateTokenHelper<TestPerm>>(), Arg.Any<IMultiLevelCacheService>(), Arg.Any<CancellationToken>())
            .Returns(new MResponse<RefreshTokenResponseModel>());
        var result = await _controller.RefreshToken(request, new MTokenInfo(), Substitute.For<MAuthenticateTokenHelper<TestPerm>>(new MTokenInfo(), Substitute.For<ITokenSigner>(), Substitute.For<IMDateTimeService>(), null), Substitute.For<IMultiLevelCacheService>(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Register_ReturnsActionResult()
    {
        var request = new RegisterRequestModel { UserName = "u", Password = "p", Email = "e@a.com", Name = "n", Surname = "s" };
        _authService.RegisterAsync(request, Arg.Any<CancellationToken>()).Returns(new MResponse<LoginResponseModel>());
        var result = await _controller.Register(request, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetMenuMetadata_ReturnsActionResult()
    {
        _permissionService.GetMenuMetadataAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<List<MenuMetadata>>());
        var result = await _controller.GetMenuMetadata(Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetUiManifest_ReturnsActionResult()
    {
        _permissionService.GetUiManifestAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<MUiManifest>());
        var result = await _controller.GetUiManifest(Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCurrentUserUiManifest_NoUser_ReturnsUnauthorized()
    {
        var result = await _controller.GetCurrentUserUiManifest(CancellationToken.None);
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetCurrentUserUiManifest_WithUser_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimConstants.UserIdentifier, userId.ToString()) }));
        _permissionService.GetUiManifestAsync(userId, Arg.Any<CancellationToken>()).Returns(new MResponse<MUiManifest> { Result = new MUiManifest() });
        
        var result = await _controller.GetCurrentUserUiManifest(CancellationToken.None);
        Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ((ObjectResult)result).StatusCode);
    }

    [Fact]
    public async Task GetUiEngineManifest_ReturnsActionResult()
    {
        _permissionService.GetUiEngineManifestAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<MUiEngineManifest> { Result = new MUiEngineManifest() });
        var result = await _controller.GetUiEngineManifest(Guid.NewGuid(), null, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetUiEngineContractInfo_ReturnsActionResult()
    {
        _permissionService.GetUiEngineContractInfoAsync(Arg.Any<CancellationToken>()).Returns(new MResponse<MUiEngineContractInfo>());
        var result = await _controller.GetUiEngineContractInfo(CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetUiEngineSchemaHash_ReturnsActionResult()
    {
        _permissionService.GetUiEngineSchemaVersionAsync(Arg.Any<CancellationToken>()).Returns(new MResponse<MUiEngineSchemaVersion>());
        var result = await _controller.GetUiEngineSchemaHash(CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task NotifyUiEngineSchemaChange_ReturnsActionResult()
    {
        var notification = new MUiEngineSchemaChangeNotification();
        _permissionService.NotifyUiEngineSchemaChangeAsync(notification, Arg.Any<CancellationToken>()).Returns(new MResponse<object>());
        var result = await _controller.NotifyUiEngineSchemaChange(notification, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetPermissionTree_ReturnsActionResult()
    {
        _permissionService.GetUserPermissionTreeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<PermissionTree>());
        var result = await _controller.GetPermissionTree(Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetUsers_ReturnsActionResult()
    {
        _permissionService.GetUsersAsync(1, 10, Arg.Any<CancellationToken>()).Returns(new MResponse<MPagedResult<MUser>>());
        var result = await _controller.GetUsers(1, 10, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetUser_ReturnsActionResult()
    {
        _permissionService.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<MUser>());
        var result = await _controller.GetUser(Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task UpdateUser_ReturnsActionResult()
    {
        var request = new MUserModel { UserGuid = Guid.NewGuid().ToString() };
        _permissionService.UpdateUserAsync(request, Arg.Any<CancellationToken>()).Returns(new MResponse<MUser>());
        var result = await _controller.UpdateUser(request, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeleteUser_ReturnsActionResult()
    {
        _permissionService.DeleteUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new MResponse<object>());
        var result = await _controller.DeleteUser(Guid.NewGuid(), CancellationToken.None);
        Assert.NotNull(result);
    }
}
