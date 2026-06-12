namespace Muonroi.AspNetCore.Controllers;

/// <inheritdoc />
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public abstract class MAuthControllerBase<TPermission, TDbContext>(
    IAuthService<TPermission, TDbContext> authService,
    IPermissionService<TPermission> permissionService) : ControllerBase
    where TPermission : Enum
    where TDbContext : MDbContext
{
/// <inheritdoc />
    [HttpPost("create-role")]
    public virtual async Task<IActionResult> CreateRole([FromBody] CreateRoleRequestModel request,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<MRole> result = await permissionService.CreateRoleAsync(request, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpPost("assign-permission")]
    public virtual async Task<IActionResult> AssignPermissionToRole([FromBody] AssignPermissionRequestModel request,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<object> result = await permissionService.AssignPermissionToRoleAsync(request, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpDelete("remove-permission/{roleId:guid}/{permissionId:guid}")]
    public virtual async Task<IActionResult> RemovePermissionFromRole(Guid roleId, Guid permissionId,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<object> result = await permissionService.RemovePermissionFromRoleAsync(roleId, permissionId, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpPost("assign-role")]
    public virtual async Task<IActionResult> AssignRoleToUser([FromBody] AssignRoleRequestModel request,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<object> result = await permissionService.AssignRoleToUserAsync(request, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("user-permissions/{userId:guid}")]
    public virtual async Task<IActionResult> GetUserPermissions(Guid userId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<List<TPermission>> result = await permissionService.GetUserPermissionsAsync(userId, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpPost("update-role")]
    public virtual async Task<IActionResult> UpdateRole([FromBody] UpdateRoleRequestModel request,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<MRole> result = await permissionService.UpdateRoleAsync(request, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpDelete("delete-role/{roleId:guid}")]
    public virtual async Task<IActionResult> DeleteRole(Guid roleId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<object> result = await permissionService.DeleteRoleAsync(roleId, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("roles")]
    public virtual async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<List<MRole>> result = await permissionService.GetRolesAsync(cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("permissions")]
    public virtual async Task<IActionResult> GetPermission(CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<List<MPermission>> result = await permissionService.GetPermissionsAsync(cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("role-permissions/{roleId:guid}")]
    public virtual async Task<IActionResult> GetRolePermissions(Guid roleId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<List<MPermission>> result = await permissionService.GetRolePermissionsAsync(roleId, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("role-users/{roleId:guid}")]
    public virtual async Task<IActionResult> GetRoleUsers(Guid roleId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<List<MUser>> result = await permissionService.GetRoleUsersAsync(roleId, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("permission-definitions")]
    public virtual async Task<IActionResult> GetPermissionDefinitions(CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<List<PermissionDefinition>> result = await permissionService.GetPermissionDefinitionsAsync(cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpPost("logout")]
    public virtual async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        MResponse<object> result = await authService.LogoutAsync(cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpPost("logout-all")]
    public virtual async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        MResponse<object> result = await authService.LogoutAllAsync(cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [AllowAnonymous]
    [HttpPost("login")]
    public virtual async Task<IActionResult> Login([FromBody] LoginRequestModel request,
        [FromServices] MTokenInfo tokenInfo,
        [FromServices] MAuthenticateTokenHelper<TPermission> tokenHelper,
        [FromServices] IMultiLevelCacheService cacheService,
        CancellationToken cancellationToken)
    {
        MResponse<LoginResponseModel> result = await authService.LoginAsync(request, tokenInfo, tokenHelper, cacheService, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public virtual async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestModel request,
        [FromServices] MTokenInfo tokenInfo,
        [FromServices] MAuthenticateTokenHelper<TPermission> tokenHelper,
        [FromServices] IMultiLevelCacheService cacheService,
        CancellationToken cancellationToken)
    {
        MResponse<RefreshTokenResponseModel> result =
            await authService.RefreshTokenAsync(request, tokenInfo, tokenHelper, cacheService, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [AllowAnonymous]
    [HttpPost("register")]
    public virtual async Task<IActionResult> Register([FromBody] RegisterRequestModel request,
        CancellationToken cancellationToken)
    {
        MResponse<LoginResponseModel> result = await authService.RegisterAsync(request, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("menu-metadata/{userId:guid}")]
    public virtual async Task<IActionResult> GetMenuMetadata(Guid userId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<List<MenuMetadata>> result = await permissionService.GetMenuMetadataAsync(userId, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("ui-manifest/{userId:guid}")]
    public virtual async Task<IActionResult> GetUiManifest(Guid userId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<MUiManifest> result = await permissionService.GetUiManifestAsync(userId, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("ui-manifest/current")]
    public virtual async Task<IActionResult> GetCurrentUserUiManifest(CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();

        string? userIdValue = User.FindFirst(ClaimConstants.UserIdentifier)?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdValue, out Guid userId))
        {
            MResponse<object> response = new();
            response.AddError("NO_USER_CONTEXT");
            return Unauthorized(response);
        }

        MResponse<MUiManifest> result = await permissionService.GetUiManifestAsync(userId, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("ui-engine/{userId:guid}")]
    public virtual async Task<IActionResult> GetUiEngineManifest(
        Guid userId,
        [FromQuery] string? minimalFor,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        ApplyAcceptLanguageOverride();
        MResponse<MUiEngineManifest> result = await permissionService.GetUiEngineManifestAsync(userId, cancellationToken);
        return BuildUiEngineManifestResult(result, minimalFor);
    }

/// <inheritdoc />
    [HttpGet("ui-engine/current")]
    public virtual async Task<IActionResult> GetCurrentUserUiEngineManifest(
        [FromQuery] string? minimalFor,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        ApplyAcceptLanguageOverride();

        string? userIdValue = User.FindFirst(ClaimConstants.UserIdentifier)?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(userIdValue, out Guid userId))
        {
            MResponse<object> response = new();
            response.AddError("NO_USER_CONTEXT");
            return Unauthorized(response);
        }

        MResponse<MUiEngineManifest> result = await permissionService.GetUiEngineManifestAsync(userId, cancellationToken);
        return BuildUiEngineManifestResult(result, minimalFor);
    }

/// <inheritdoc />
    [AllowAnonymous]
    [HttpGet("ui-engine/contract-info")]
    public virtual async Task<IActionResult> GetUiEngineContractInfo(CancellationToken cancellationToken)
    {
        MResponse<MUiEngineContractInfo> result = await permissionService.GetUiEngineContractInfoAsync(cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [AllowAnonymous]
    [HttpGet("ui-engine/schema-hash")]
    public virtual async Task<IActionResult> GetUiEngineSchemaHash(CancellationToken cancellationToken)
    {
        MResponse<MUiEngineSchemaVersion> result = await permissionService.GetUiEngineSchemaVersionAsync(cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [AllowAnonymous]
    [HttpPost("ui-engine/notify-change")]
    public virtual async Task<IActionResult> NotifyUiEngineSchemaChange(
        [FromBody] MUiEngineSchemaChangeNotification notification,
        CancellationToken cancellationToken)
    {
        MResponse<object> result = await permissionService.NotifyUiEngineSchemaChangeAsync(notification, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("permission-tree/{userId:guid}")]
    public virtual async Task<IActionResult> GetPermissionTree(Guid userId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<PermissionTree> result = await permissionService.GetUserPermissionTreeAsync(userId, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("users")]
    public virtual async Task<IActionResult> GetUsers([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<MPagedResult<MUser>> result = await permissionService.GetUsersAsync(pageIndex, pageSize, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpGet("users/{userId:guid}")]
    public virtual async Task<IActionResult> GetUser(Guid userId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<MUser> result = await permissionService.GetUserAsync(userId, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpPut("users")]
    public virtual async Task<IActionResult> UpdateUser([FromBody] MUserModel request, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<MUser> result = await permissionService.UpdateUserAsync(request, cancellationToken);
        return result.GetActionResult();
    }

/// <inheritdoc />
    [HttpDelete("users/{userId:guid}")]
    public virtual async Task<IActionResult> DeleteUser(Guid userId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthLicensed();
        MResponse<object> result = await permissionService.DeleteUserAsync(userId, cancellationToken);
        return result.GetActionResult();
    }

    /// <summary>
    /// Ensures the AdvancedAuth feature is licensed for the current tenant.
    /// </summary>
    protected virtual void EnsureAdvancedAuthLicensed()
    {
        ILicenseGuard? guard = HttpContext?.RequestServices.GetRequiredService<ILicenseGuard>();
        guard?.EnsureFeature(FreeTierFeatures.Premium.AdvancedAuth);
    }

    private IActionResult BuildUiEngineManifestResult(
        MResponse<MUiEngineManifest> response,
        string? minimalFor)
    {
        if (response.Result is null)
        {
            return response.GetActionResult();
        }

        response.Result = BuildProjectedManifest(response.Result, minimalFor);

        string etag = BuildManifestEtag(response.Result);
        Response.Headers.ETag = etag;
        string requestEtag = Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrWhiteSpace(requestEtag) &&
            string.Equals(requestEtag.Trim(), etag, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return response.GetActionResult();
    }

    private static string BuildManifestEtag(MUiEngineManifest manifest)
    {
        MUiEngineManifest normalized = CloneForEtag(manifest);
        string payload = System.Text.Json.JsonSerializer.Serialize(normalized, new JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = false
        }); // MBB002-exempt: requires custom JsonOptions (CamelCase + WriteIndented=false) for ETag hash not available in wrapper
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        return $"\"{Convert.ToHexString(hash).ToLowerInvariant()}\"";
    }

    private static MUiEngineManifest CloneForEtag(MUiEngineManifest manifest)
    {
        return new MUiEngineManifest
        {
            SchemaVersion = manifest.SchemaVersion,
            GeneratedAtUtc = DateTime.UnixEpoch,
            UserId = manifest.UserId,
            TenantId = manifest.TenantId,
            LicenseTier = manifest.LicenseTier,
            Capabilities = manifest.Capabilities,
            NavigationGroups = manifest.NavigationGroups,
            Screens = manifest.Screens,
            Actions = manifest.Actions,
            DataSources = manifest.DataSources,
            ComponentRegistry = manifest.ComponentRegistry,
            AppShell = manifest.AppShell,
            AuthProfile = manifest.AuthProfile,
            ApiContracts = manifest.ApiContracts,
            RuleBindings = manifest.RuleBindings,
            GenerationHints = manifest.GenerationHints
        };
    }

    private static MUiEngineManifest BuildProjectedManifest(MUiEngineManifest manifest, string? minimalFor)
    {
        if (!string.Equals(minimalFor, "routing", StringComparison.OrdinalIgnoreCase))
        {
            return manifest;
        }

        MUiEngineManifest projected = new()
        {
            SchemaVersion = manifest.SchemaVersion,
            GeneratedAtUtc = manifest.GeneratedAtUtc,
            UserId = manifest.UserId,
            TenantId = manifest.TenantId,
            LicenseTier = manifest.LicenseTier,
            NavigationGroups = [.. manifest.NavigationGroups.Select(MapNavigationGroupForRouting)],
            Screens = [.. manifest.Screens.Select(screen => new MUiEngineScreen
            {
                ScreenKey = screen.ScreenKey,
                UiKey = screen.UiKey,
                Title = screen.Title,
                Route = screen.Route,
                IsVisible = screen.IsVisible,
                IsEnabled = screen.IsEnabled
            })]
        };

        return projected;
    }

    private static MUiEngineNavigationGroup MapNavigationGroupForRouting(MUiEngineNavigationGroup group)
    {
        return new MUiEngineNavigationGroup
        {
            GroupName = group.GroupName,
            GroupDisplayName = group.GroupDisplayName,
            Items = [.. group.Items.Select(MapNavigationNodeForRouting)]
        };
    }

    private static MUiEngineNavigationNode MapNavigationNodeForRouting(MUiEngineNavigationNode node)
    {
        return new MUiEngineNavigationNode
        {
            NodeKey = node.NodeKey,
            UiKey = node.UiKey,
            Title = node.Title,
            Route = node.Route,
            Type = node.Type,
            Icon = node.Icon,
            Order = node.Order,
            IsVisible = node.IsVisible,
            IsEnabled = node.IsEnabled,
            ScreenKey = node.ScreenKey,
            ActionKeys = [.. node.ActionKeys],
            Children = [.. node.Children.Select(MapNavigationNodeForRouting)]
        };
    }

    private void ApplyAcceptLanguageOverride()
    {
        string language = Request.Headers.AcceptLanguage.ToString();
        if (string.IsNullOrWhiteSpace(language))
        {
            return;
        }

        MAuthenticateInfoContext? authContext = HttpContext?.RequestServices.GetService<MAuthenticateInfoContext>();
        if (authContext is null)
        {
            return;
        }

        string normalized = language.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? language;
        authContext.Language = normalized;
    }
}

