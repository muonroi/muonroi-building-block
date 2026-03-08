using Muonroi.Core.Timing;

namespace Muonroi.AspNetCore.Services;

/// <summary>
/// Service for managing permissions, roles, and user-role associations.
/// </summary>
/// <typeparam name="TPermission">The type of the permission enum.</typeparam>
/// <typeparam name="TDbContext">The type of the database context.</typeparam>
public class PermissionService<TPermission, TDbContext>(
    TDbContext dbContext,
    MAuthenticateInfoContext context,
    IMDateTimeService dateTimeService,
    IMultiLevelCacheService? cacheService = null,
    ILicenseGuard? licenseGuard = null,
    ITenantQuotaStore? tenantQuotaStore = null,
    IUiEngineSchemaNotifier? uiEngineSchemaNotifier = null,
    IEnumerable<IUiEngineManifestContributor>? uiEngineManifestContributors = null,
    PermissionQueryService<TDbContext>? permissionQueryService = null,
    UiManifestBuilder? uiManifestBuilder = null,
    UiEngineManifestOrchestrator<TDbContext>? uiEngineManifestOrchestrator = null,
    UiEngineSchemaVersionService<TDbContext>? uiEngineSchemaVersionService = null,
    UiEngineCapabilityResolver? uiEngineCapabilityResolver = null,
    IServiceProvider? serviceProvider = null) : IPermissionService<TPermission>
    where TPermission : Enum
    where TDbContext : MDbContext
{
    /// <summary>
    /// Creates a new role asynchronously.
    /// </summary>
    /// <param name="request">The create role request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MRole}"/> containing the created role.</returns>
    public async Task<MResponse<MRole>> CreateRoleAsync(CreateRoleRequestModel request,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<MRole> result = new();
        PermissionQueryService<TDbContext> queryService = permissionQueryService ?? new PermissionQueryService<TDbContext>(dbContext);
        MRole? existingRole = await queryService.GetRoleByNameAsync(request.Name, cancellationToken);
        if (existingRole != null)
        {
            result.AddError(nameof(SystemEnum.RoleAlreadyExists), context.Language, [existingRole.Name]);
            return result;
        }

        MRole role = new()
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            IsStatic = request.IsStatic,
            IsDefault = request.IsDefault,
            NormalizedName = request.Name.ToUpperInvariant()
        };

        _ = await dbContext.Set<MRole>().AddAsync(role, cancellationToken);
        _ = await dbContext.SaveChangesAsync(cancellationToken);

        result.Result = role;
        return result;
    }

    /// <summary>
    /// Assigns a permission to a role asynchronously.
    /// </summary>
    /// <param name="request">The assign permission request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    public async Task<MResponse<object>> AssignPermissionToRoleAsync(AssignPermissionRequestModel request,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<object> result = new();
        PermissionQueryService<TDbContext> queryService = permissionQueryService ?? new PermissionQueryService<TDbContext>(dbContext);
        MRole? role = await queryService.GetRoleByIdAsync(request.RoleId, cancellationToken);
        MPermission? permission = await queryService.GetPermissionByIdAsync(request.PermissionId, cancellationToken);

        if (role == null || permission == null)
        {
            result.AddError(
                role == null ? nameof(SystemEnum.RoleNotFound) : nameof(SystemEnum.PermissionNotFound),
                context.Language);
            return result;
        }

        MRolePermission? rolePermission = await queryService.GetRolePermissionAsync(
            role.EntityId,
            permission.EntityId,
            cancellationToken);
        if (rolePermission != null)
        {
            result.AddError(nameof(SystemEnum.RoleAlreadyHasPermission), context.Language);
            return result;
        }

        MRolePermission newRolePermission = new()
        {
            RoleId = role.EntityId,
            PermissionId = permission.EntityId
        };
        _ = await dbContext.Set<MRolePermission>().AddAsync(newRolePermission, cancellationToken);

        Guid? userId = Guid.TryParse(context.CurrentUserGuid, out Guid parsed) ? parsed : null;
        MPermissionAuditLog audit = new()
        {
            RoleId = role.EntityId,
            PermissionId = permission.EntityId,
            Action = "Assign",
            PerformedBy = userId
        };
        _ = dbContext.Set<MPermissionAuditLog>().Add(audit);

        _ = await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePermissionCacheForRoleAsync(role.EntityId, cancellationToken);
        return result;
    }

    /// <summary>
    /// Removes a permission from a role asynchronously.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="permissionId">The unique identifier of the permission.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    public async Task<MResponse<object>> RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<object> result = new();
        MRolePermission? rolePermission = await dbContext.Set<MRolePermission>()
            .FirstOrDefaultAsync(x => x.RoleId == roleId && x.PermissionId == permissionId && !x.IsDeleted,
                cancellationToken);
        if (rolePermission == null)
        {
            result.AddError(nameof(SystemEnum.RolePermissionNotFound), context.Language);
            return result;
        }

        rolePermission.IsDeleted = true;
        dbContext.Set<MRolePermission>().Update(rolePermission);

        Guid? userId = Guid.TryParse(context.CurrentUserGuid, out Guid parsed) ? parsed : null;
        MPermissionAuditLog audit = new()
        {
            RoleId = rolePermission.RoleId,
            PermissionId = rolePermission.PermissionId,
            Action = "Remove",
            PerformedBy = userId
        };
        _ = dbContext.Set<MPermissionAuditLog>().Add(audit);

        _ = await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePermissionCacheForRoleAsync(rolePermission.RoleId, cancellationToken);
        return result;
    }

    /// <summary>
    /// Assigns a role to a user asynchronously.
    /// </summary>
    /// <param name="request">The assign role request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    public async Task<MResponse<object>> AssignRoleToUserAsync(AssignRoleRequestModel request,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<object> result = new();
        PermissionQueryService<TDbContext> queryService = permissionQueryService ?? new PermissionQueryService<TDbContext>(dbContext);
        MRole? role = await queryService.GetRoleByIdAsync(request.RoleId, cancellationToken);
        MUser? user = await queryService.GetUserByIdAsync(request.UserId, cancellationToken);
        if (role == null || user == null)
        {
            result.AddError(role == null ? nameof(SystemEnum.RoleNotFound) : nameof(SystemEnum.UserNotFound), context.Language);
            return result;
        }

        MUserRole? userRole = await queryService.GetUserRoleAsync(user.EntityId, role.EntityId, cancellationToken);
        if (userRole != null)
        {
            result.AddError(nameof(SystemEnum.UserAlreadyHasRole), context.Language);
            return result;
        }

        MUserRole newUserRole = new()
        {
            RoleId = role.EntityId,
            UserId = user.EntityId
        };
        _ = await dbContext.Set<MUserRole>().AddAsync(newUserRole, cancellationToken);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePermissionCacheForUsersAsync([(user.EntityId, user.Id)], cancellationToken);
        return result;
    }

    /// <summary>
    /// Gets the permissions granted to a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{TPermission}}"/> containing the user's permissions.</returns>
    public async Task<MResponse<List<TPermission>>> GetUserPermissionsAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<List<TPermission>> result = new();
        List<TPermission> userPermissions = await GetPermissionsOfUserAsync(userId, cancellationToken);
        if (userPermissions.Count == 0)
        {
            result.AddError(nameof(SystemEnum.UserHasNoPermissions), context.Language);
        }
        else
        {
            result.Result = userPermissions;
        }

        return result;
    }

    /// <summary>
    /// Updates an existing role asynchronously.
    /// </summary>
    /// <param name="request">The update role request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MRole}"/> containing the updated role.</returns>
    public async Task<MResponse<MRole>> UpdateRoleAsync(UpdateRoleRequestModel request,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<MRole> result = new();
        MRole? role = await dbContext.Set<MRole>()
            .FirstOrDefaultAsync(x => x.EntityId == request.Id && !x.IsDeleted, cancellationToken);
        if (role == null)
        {
            result.AddError(nameof(SystemEnum.RoleNotFound), context.Language);
            return result;
        }

        role.Name = request.Name;
        role.DisplayName = request.DisplayName;
        role.IsStatic = request.IsStatic;
        role.IsDefault = request.IsDefault;
        role.NormalizedName = request.Name.ToUpperInvariant();

        _ = await dbContext.SaveChangesAsync(cancellationToken);
        result.Result = role;
        return result;
    }

    /// <summary>
    /// Deletes a role asynchronously.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    public async Task<MResponse<object>> DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<object> result = new();
        MRole? role = await dbContext.Set<MRole>()
            .FirstOrDefaultAsync(x => x.EntityId == roleId, cancellationToken);
        if (role == null)
        {
            result.AddError(nameof(SystemEnum.RoleNotFound), context.Language);
            return result;
        }

        role.IsDeleted = true;
        dbContext.Update(role);
        _ = await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePermissionCacheForRoleAsync(role.EntityId, cancellationToken);
        return result;
    }

    /// <summary>
    /// Gets all roles asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{MRole}}"/> containing all roles.</returns>
    public async Task<MResponse<List<MRole>>> GetRolesAsync(CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        PermissionQueryService<TDbContext> queryService = permissionQueryService ?? new PermissionQueryService<TDbContext>(dbContext);
        MResponse<List<MRole>> result = new();
        List<MRole> roles = await queryService.GetRolesAsync(cancellationToken);
        result.Result = roles;
        return result;
    }

    /// <summary>
    /// Gets all permissions asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{MPermission}}"/> containing all permissions.</returns>
    public async Task<MResponse<List<MPermission>>> GetPermissionsAsync(CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        PermissionQueryService<TDbContext> queryService = permissionQueryService ?? new PermissionQueryService<TDbContext>(dbContext);
        MResponse<List<MPermission>> result = new();
        List<MPermission> permissions = await queryService.GetPermissionsAsync(cancellationToken);
        result.Result = permissions;
        return result;
    }

    /// <summary>
    /// Gets permissions associated with a specific role asynchronously.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{MPermission}}"/> containing the role's permissions.</returns>
    public async Task<MResponse<List<MPermission>>> GetRolePermissionsAsync(Guid roleId,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        PermissionQueryService<TDbContext> queryService = permissionQueryService ?? new PermissionQueryService<TDbContext>(dbContext);
        MResponse<List<MPermission>> result = new();
        List<MPermission> permissions = await queryService.GetPermissionsOfRoleAsync(roleId, cancellationToken);
        result.Result = permissions;
        return result;
    }

    /// <summary>
    /// Gets users associated with a specific role asynchronously.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{MUser}}"/> containing the role's users.</returns>
    public async Task<MResponse<List<MUser>>> GetRoleUsersAsync(Guid roleId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        PermissionQueryService<TDbContext> queryService = permissionQueryService ?? new PermissionQueryService<TDbContext>(dbContext);
        MResponse<List<MUser>> result = new();
        List<MUser> users = await queryService.GetUsersOfRoleAsync(roleId, cancellationToken);
        result.Result = users;
        return result;
    }

    /// <summary>
    /// Gets definitions for all available permissions asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{PermissionDefinition}}"/> containing permission definitions.</returns>
    public async Task<MResponse<List<PermissionDefinition>>> GetPermissionDefinitionsAsync(
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<List<PermissionDefinition>> result = new();

        List<PermissionDefinition> definitions = await dbContext.Set<MPermission>()
            .AsNoTracking()
            .Include(p => p.PermissionGroup)
            .Where(p => !p.IsDeleted)
            .Select(p => new PermissionDefinition
            {
                Name = p.Name,
                UiKey = p.UiKey,
                Type = p.Type,
                GroupName = p.PermissionGroup != null ? p.PermissionGroup.Name : string.Empty,
                GroupDisplayName = p.PermissionGroup != null ? p.PermissionGroup.DisplayName : string.Empty,
                Order = p.Order ?? 0,
                Icon = p.Icon,
                Description = p.Description,
                DisplayNames = new Dictionary<string, string> { { context.Language, p.Label ?? p.Name } },
                AllowedProviders = new List<string> { "User", "Role" }
            }).ToListAsync(cancellationToken);

        result.Result = definitions;
        return result;
    }

    /// <summary>
    /// Logs out the current user by revoking their active refresh token asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    public async Task<MResponse<object>> LogoutAsync(CancellationToken cancellationToken)
    {
        MResponse<object> result = new();

        if (!Guid.TryParse(context.CurrentUserGuid, out Guid userGuid))
        {
            result.AddError(nameof(SystemEnum.UserNotFound), context.Language);
            return result;
        }

        // Try to use ExecuteUpdateAsync for single roundtrip, fallback if not supported
        int rowsAffected;
        try
        {
            rowsAffected = await dbContext.Set<MRefreshToken>()
                .Where(rt => rt.TokenValidityKey == context.TokenValidityKey
                             && rt.CreatorUserId == userGuid
                             && !rt.IsDeleted
                             && !rt.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedDate, Clock.UtcNow)
                    .SetProperty(rt => rt.ReasonRevoked, "Logout"), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            List<MRefreshToken> tokens = await dbContext.Set<MRefreshToken>()
                .Where(rt => rt.TokenValidityKey == context.TokenValidityKey
                             && rt.CreatorUserId == userGuid
                             && !rt.IsDeleted
                             && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (MRefreshToken? token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedDate = Clock.UtcNow;
                token.ReasonRevoked = "Logout";
            }

            rowsAffected = await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (rowsAffected == 0)
        {
            result.AddError(nameof(SystemEnum.InvalidCredentials), context.Language);
        }

        return result;
    }

    /// <summary>
    /// Logs out the current user from all devices by revoking all their refresh tokens asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    public async Task<MResponse<object>> LogoutAllAsync(CancellationToken cancellationToken)
    {
        MResponse<object> result = new();

        if (!Guid.TryParse(context.CurrentUserGuid, out Guid userGuid))
        {
            result.AddError(nameof(SystemEnum.UserNotFound), context.Language);
            return result;
        }

        try
        {
            _ = await dbContext.Set<MRefreshToken>()
                .Where(rt => rt.CreatorUserId == userGuid && !rt.IsDeleted && !rt.IsRevoked)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedDate, Clock.UtcNow)
                    .SetProperty(rt => rt.ReasonRevoked, "LogoutAll"), cancellationToken);
        }
        catch (InvalidOperationException)
        {
            List<MRefreshToken> tokens = await dbContext.Set<MRefreshToken>()
                .Where(rt => rt.CreatorUserId == userGuid && !rt.IsDeleted && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (MRefreshToken? token in tokens)
            {
                token.IsRevoked = true;
                token.RevokedDate = Clock.UtcNow;
                token.ReasonRevoked = "LogoutAll";
                _ = dbContext.Update(token);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Gets menu metadata for a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{MenuMetadata}}"/> containing menu metadata.</returns>
    public async Task<MResponse<List<MenuMetadata>>> GetMenuMetadataAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<List<MenuMetadata>> result = new();

        List<Guid> grantedIds = await (from userRole in dbContext.Set<MUserRole>().AsNoTracking()
                                       join rolePermission in dbContext.Set<MRolePermission>().AsNoTracking() on userRole.RoleId equals
                                           rolePermission.RoleId
                                       where userRole.UserId == userId && !userRole.IsDeleted && !rolePermission.IsDeleted
                                       select rolePermission.PermissionId).Distinct().ToListAsync(cancellationToken);

        List<MenuMetadata> metadata = await dbContext.Set<MPermission>()
            .AsNoTracking()
            .Include(p => p.PermissionGroup)
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.PermissionGroup)
            .Select(g => new MenuMetadata
            {
                GroupName = g.Key != null ? g.Key.Name : string.Empty,
                GroupDisplayName = g.Key != null ? g.Key.DisplayName : string.Empty,
                Actions = g.Where(p => p.ParentId == null).Select(p => new ActionMetadata
                {
                    Name = p.Name,
                    UiKey = p.UiKey,
                    Type = p.Type,
                    IsGranted = grantedIds.Contains(p.EntityId),
                    Children = g.Where(c => c.ParentId == p.EntityId).Select(c => new ActionMetadata
                    {
                        Name = c.Name,
                        UiKey = c.UiKey,
                        Type = c.Type,
                        IsGranted = grantedIds.Contains(c.EntityId)
                    }).ToList()
                }).ToList()
            }).ToListAsync(cancellationToken);

        result.Result = metadata;
        return result;
    }

    /// <summary>
    /// Gets the UI manifest for a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUiManifest}"/> containing the UI manifest.</returns>
    public async Task<MResponse<MUiManifest>> GetUiManifestAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        PermissionQueryService<TDbContext> queryService = permissionQueryService ?? new PermissionQueryService<TDbContext>(dbContext);
        _ = uiManifestBuilder ?? new UiManifestBuilder();

        List<Guid> grantedIds = await queryService.GetGrantedPermissionIdsAsync(userId, cancellationToken);
        List<MPermission> permissions = await queryService.GetAllPermissionsWithGroupsAsync(cancellationToken);

        MUiManifest manifest = UiManifestBuilder.BuildUiManifest(
            userId,
            TenantContext.CurrentTenantId,
            permissions,
            new HashSet<Guid>(grantedIds));

        MResponse<MUiManifest> result = new()
        {
            Result = manifest
        };
        return result;
    }

    /// <summary>
    /// Gets the UI engine manifest for a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUiEngineManifest}"/> containing the UI engine manifest.</returns>
    public async Task<MResponse<MUiEngineManifest>> GetUiEngineManifestAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();

        List<IUiEngineManifestContributor> contributors = [.. uiEngineManifestContributors ?? []];

        PermissionQueryService<TDbContext> queryService = permissionQueryService ?? new PermissionQueryService<TDbContext>(dbContext);
        UiEngineCapabilityResolver capabilityResolver =
            uiEngineCapabilityResolver ?? new UiEngineCapabilityResolver(contributors);
        UiEngineManifestOrchestrator<TDbContext> orchestrator = uiEngineManifestOrchestrator ??
            new UiEngineManifestOrchestrator<TDbContext>(
                queryService,
                capabilityResolver,
                contributors,
                serviceProvider,
                tenantQuotaStore);

        MResponse<MUiEngineManifest> result = new()
        {
            Result = await orchestrator.BuildAsync(userId, cancellationToken)
        };
        return result;
    }

    /// <summary>
    /// Gets the UI engine contract information asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUiEngineContractInfo}"/> containing contract information.</returns>
    public Task<MResponse<MUiEngineContractInfo>> GetUiEngineContractInfoAsync(CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        MResponse<MUiEngineContractInfo> result = new()
        {
            Result = new MUiEngineContractInfo()
        };

        return Task.FromResult(result);
    }

    /// <summary>
    /// Gets the UI engine schema version asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUiEngineSchemaVersion}"/> containing the schema version.</returns>
    public async Task<MResponse<MUiEngineSchemaVersion>> GetUiEngineSchemaVersionAsync(CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        IMJsonSerializeService serializer = serviceProvider?.GetService<IMJsonSerializeService>() ?? new MJsonSerializeService();
        UiEngineSchemaVersionService<TDbContext> schemaService =
            uiEngineSchemaVersionService ?? new UiEngineSchemaVersionService<TDbContext>(dbContext, dateTimeService, serializer);

        MResponse<MUiEngineSchemaVersion> result = new()
        {
            Result = await schemaService.BuildUiEngineSchemaVersionAsync(cancellationToken)
        };
        return result;
    }

    /// <summary>
    /// Notifies the UI engine of a schema change asynchronously.
    /// </summary>
    /// <param name="notification">The schema change notification details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result and containing the current schema version info.</returns>
    public async Task<MResponse<object>> NotifyUiEngineSchemaChangeAsync(
        MUiEngineSchemaChangeNotification notification,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();

        IMJsonSerializeService serializer = serviceProvider?.GetService<IMJsonSerializeService>() ?? new MJsonSerializeService();
        UiEngineSchemaVersionService<TDbContext> schemaService =
            uiEngineSchemaVersionService ?? new UiEngineSchemaVersionService<TDbContext>(dbContext, dateTimeService, serializer);
        MUiEngineSchemaVersion schemaVersion = await schemaService.BuildUiEngineSchemaVersionAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(notification?.SchemaHash))
        {
            string normalized = notification.SchemaHash.Trim().ToLowerInvariant();
            schemaVersion.SchemaHash = normalized;
            schemaVersion.OpenApiHash = normalized;
        }

        schemaVersion.GeneratedAtUtc = notification?.ChangedAtUtc?.ToUniversalTime() ?? dateTimeService.UtcNow();

        if (uiEngineSchemaNotifier is not null)
        {
            await uiEngineSchemaNotifier.NotifySchemaChangedAsync(schemaVersion, cancellationToken);
        }

        MResponse<object> result = new()
        {
            Result = new
            {
                schemaVersion.SchemaHash,
                schemaVersion.OpenApiHash,
                schemaVersion.Version,
                schemaVersion.GeneratedAtUtc,
                source = notification?.Source ?? "manual",
                notifiedRealtimeClients = uiEngineSchemaNotifier is not null
            }
        };

        return result;
    }

    /// <summary>
    /// Gets the permission tree for a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{PermissionTree}"/> containing the user's permission tree.</returns>
    public async Task<MResponse<PermissionTree>> GetUserPermissionTreeAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<PermissionTree> result = new();

        List<string> permissionNames = await GetPermissionNamesOfUserAsync(userId, cancellationToken);

        PermissionTree tree = BuildPermissionTree(permissionNames);
        result.Result = tree;
        return result;
    }

    /// <summary>
    /// Gets a paged list of users asynchronously.
    /// </summary>
    /// <param name="pageIndex">The page index (1-based).</param>
    /// <param name="pageSize">The number of users per page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MPagedResult{MUser}}"/> containing the paged list of users.</returns>
    public async Task<MResponse<MPagedResult<MUser>>> GetUsersAsync(int pageIndex, int pageSize,
        CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<MPagedResult<MUser>> result = new();
        IQueryable<MUser> query = dbContext.Set<MUser>().AsNoTracking().Where(x => !x.IsDeleted);
        List<MUser> users = await query
            .OrderByDescending(x => x.CreationTime)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        int total = await query.CountAsync(cancellationToken);

        result.Result = new MPagedResult<MUser>
        {
            Items = users,
            CurrentPage = pageIndex,
            PageSize = pageSize,
            RowCount = total
        };
        return result;
    }

    /// <summary>
    /// Gets a user by their unique identifier asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUser}"/> containing the user details.</returns>
    public async Task<MResponse<MUser>> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<MUser> result = new();
        MUser? user = await dbContext.Set<MUser>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EntityId == userId && !x.IsDeleted, cancellationToken);

        if (user == null)
        {
            result.AddError(nameof(SystemEnum.UserNotFound), context.Language);
            return result;
        }

        result.Result = user;
        return result;
    }

    /// <summary>
    /// Updates user information asynchronously.
    /// </summary>
    /// <param name="request">The user update request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUser}"/> containing the updated user details.</returns>
    public async Task<MResponse<MUser>> UpdateUserAsync(MUserModel request, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<MUser> result = new();

        // Validate UserGuid format
        if (!Guid.TryParse(request.UserGuid, out Guid userGuid))
        {
            result.AddError(nameof(SystemEnum.InvalidRequest), context.Language);
            return result;
        }

        MUser? user = await dbContext.Set<MUser>()
            .FirstOrDefaultAsync(x => x.EntityId == userGuid && !x.IsDeleted, cancellationToken);

        if (user == null)
        {
            result.AddError(nameof(SystemEnum.UserNotFound), context.Language);
            return result;
        }

        // Check email uniqueness if email is being changed
        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !string.Equals(user.EmailAddress, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            bool emailExists = await dbContext.Set<MUser>()
                .AsNoTracking()
                .AnyAsync(x => x.NormalizedEmailAddress.Equals(request.Email, StringComparison.InvariantCultureIgnoreCase)
                               && x.EntityId != userGuid
                               && !x.IsDeleted, cancellationToken);

            if (emailExists)
            {
                result.AddError(nameof(SystemEnum.EmailAlreadyExists), context.Language);
                return result;
            }
        }

        // Update user properties
        user.Name = request.Name;
        user.Surname = request.Surname;
        user.EmailAddress = request.Email;
        user.PhoneNumber = request.PhoneNumber;

        // Set audit trail
        user.LastModificationTime = dateTimeService.UtcNow();
        if (Guid.TryParse(context.CurrentUserGuid, out Guid currentUserId))
        {
            user.LastModificationUserId = currentUserId;
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken);

        result.Result = user;
        return result;
    }

    /// <summary>
    /// Deletes a user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    public async Task<MResponse<object>> DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureAdvancedAuthFeature();
        MResponse<object> result = new();
        MUser? user = await dbContext.Set<MUser>()
            .FirstOrDefaultAsync(x => x.EntityId == userId && !x.IsDeleted, cancellationToken);

        if (user == null)
        {
            result.AddError(nameof(SystemEnum.UserNotFound), context.Language);
            return result;
        }

        // Soft delete with audit trail
        user.IsDeleted = true;
        user.DeletionTime = dateTimeService.UtcNow();
        if (Guid.TryParse(context.CurrentUserGuid, out Guid currentUserId))
        {
            user.DeletedUserId = currentUserId;
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePermissionCacheForUsersAsync([(user.EntityId, user.Id)], cancellationToken);

        return result;
    }

    private void EnsureAdvancedAuthFeature()
    {
        licenseGuard?.EnsureFeature(FreeTierFeatures.Premium.AdvancedAuth);
    }

    private async Task InvalidatePermissionCacheForRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        if (cacheService == null)
        {
            return;
        }

        var affectedUsers = await (from userRole in dbContext.Set<MUserRole>().AsNoTracking()
                                   join user in dbContext.Set<MUser>().AsNoTracking() on userRole.UserId equals user.EntityId
                                   where userRole.RoleId == roleId && !userRole.IsDeleted && !user.IsDeleted
                                   select new { user.EntityId, user.Id }).Distinct().ToListAsync(cancellationToken);

        List<(Guid EntityId, long Id)> keyPairs = [.. affectedUsers.Select(user => (user.EntityId, user.Id))];
        await InvalidatePermissionCacheForUsersAsync(keyPairs, cancellationToken);
    }

    private async Task InvalidatePermissionCacheForUsersAsync(
        IEnumerable<(Guid EntityId, long NumericId)> users,
        CancellationToken cancellationToken)
    {
        if (cacheService == null)
        {
            return;
        }

        foreach ((Guid EntityId, long NumericId) in users.DistinctBy(x => x.EntityId))
        {
            await cacheService.RemoveAsync(RbacCacheKeys.UserPermissionsByEntityId(EntityId), cancellationToken);
            await cacheService.RemoveAsync(RbacCacheKeys.LegacyUserPermissions(EntityId), cancellationToken);

            if (NumericId > 0)
            {
                await cacheService.RemoveAsync(RbacCacheKeys.UserPermissionsByNumericId(NumericId),
                    cancellationToken);
                await cacheService.RemoveAsync(RbacCacheKeys.LegacyUserPermissions(NumericId),
                    cancellationToken);
            }
        }
    }

    private async Task<List<TPermission>> GetPermissionsOfUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        List<string> permissionNames = await (from user in dbContext.Set<MUser>().AsNoTracking()
                                              join userRole in dbContext.Set<MUserRole>().AsNoTracking() on user.EntityId equals userRole.UserId
                                              join role in dbContext.Set<MRole>().AsNoTracking() on userRole.RoleId equals role.EntityId
                                              join rolePermission in dbContext.Set<MRolePermission>().AsNoTracking() on role.EntityId equals
                                                  rolePermission.RoleId
                                              join permission in dbContext.Set<MPermission>().AsNoTracking() on rolePermission.PermissionId equals
                                                  permission.EntityId
                                              where user.EntityId == userId
                                                    && !permission.IsDeleted
                                                    && !rolePermission.IsDeleted
                                                    && !role.IsDeleted
                                                    && !user.IsDeleted
                                              select permission.Name).Distinct().ToListAsync(cancellationToken);

        return
        [
            .. permissionNames
                .Where(name => Enum.TryParse(typeof(TPermission), name, out _))
                .Select(name => (TPermission)Enum.Parse(typeof(TPermission), name))
        ];
    }

    private static bool ApplyContainerVisibility(MUiManifestItem item)
    {
        if (item.Children.Count == 0)
        {
            return item.IsVisible;
        }

        bool hasVisibleChild = false;
        foreach (var _ in from MUiManifestItem child in item.Children
                          where ApplyContainerVisibility(child)
                          select new { })
        {
            hasVisibleChild = true;
        }

        if ((item.Type == PermissionType.Menu || item.Type == PermissionType.Tab) &&
            item.IsPublished &&
            hasVisibleChild)
        {
            item.IsVisible = true;
        }

        return item.IsVisible;
    }

    private static void ApplyCapabilityGuardToNodes(
        List<MUiEngineNavigationNode> nodes,
        IReadOnlySet<string> disabledScreenKeys)
    {
        foreach (MUiEngineNavigationNode node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.ScreenKey) && disabledScreenKeys.Contains(node.ScreenKey))
            {
                node.IsEnabled = false;
                node.DisabledReason ??= "tier_upgrade_required";
            }

            ApplyCapabilityGuardToNodes(node.Children, disabledScreenKeys);
        }
    }

    private static List<MUiManifestItem> FlattenUiManifestItems(MUiManifestItem root)
    {
        List<MUiManifestItem> result = [root];
        foreach (MUiManifestItem child in root.Children)
        {
            result.AddRange(FlattenUiManifestItems(child));
        }

        return result;
    }

    private static MUiEngineNavigationNode BuildNavigationNode(
        MUiManifestItem item,
        IReadOnlyDictionary<string, string> screenKeyByUiKey)
    {
        MUiEngineNavigationNode node = new()
        {
            NodeKey = MUiEngineKeyBuilder.BuildNodeKey(item.UiKey),
            UiKey = item.UiKey,
            ParentUiKey = item.ParentUiKey,
            Title = item.DisplayName,
            Route = item.Route,
            Type = item.Type,
            Icon = item.Icon,
            Order = item.Order,
            IsVisible = item.IsVisible,
            IsEnabled = item.IsEnabled,
            DisabledReason = item.DisabledReason,
            ScreenKey = screenKeyByUiKey.TryGetValue(item.UiKey, out string? screenKey) ? screenKey : null,
            ActionKeys = CollectActionKeys(item),
            Children = [.. item.Children
                .OrderBy(x => x.Order)
                .ThenBy(x => x.UiKey, StringComparer.OrdinalIgnoreCase)
                .Select(x => BuildNavigationNode(x, screenKeyByUiKey))]
        };
        return node;
    }

    private static List<string> CollectActionKeys(MUiManifestItem item)
    {
        return [.. FlattenUiManifestItems(item)
            .Where(x => x.Type == PermissionType.Action)
            .Select(x => MUiEngineKeyBuilder.BuildActionKey(x.UiKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }

    private async Task<List<string>> GetPermissionNamesOfUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await (from user in dbContext.Set<MUser>().AsNoTracking()
                      join userRole in dbContext.Set<MUserRole>().AsNoTracking() on user.EntityId equals userRole.UserId
                      join role in dbContext.Set<MRole>().AsNoTracking() on userRole.RoleId equals role.EntityId
                      join rolePermission in dbContext.Set<MRolePermission>().AsNoTracking() on role.EntityId equals
                          rolePermission.RoleId
                      join permission in dbContext.Set<MPermission>().AsNoTracking() on rolePermission.PermissionId equals
                          permission.EntityId
                      where user.EntityId == userId
                            && !permission.IsDeleted
                            && !rolePermission.IsDeleted
                            && !role.IsDeleted
                            && !user.IsDeleted
                      select permission.Name).Distinct().ToListAsync(cancellationToken);
    }

    private static PermissionTree BuildPermissionTree(IEnumerable<string> permissionNames)
    {
        PermissionTree tree = new();
        foreach (string name in permissionNames)
        {
            string[] parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            string menuKey = parts[0];
            MenuPermission menu = tree.Menus.FirstOrDefault(m => m.Key == menuKey) ?? new MenuPermission { Key = menuKey };
            if (!tree.Menus.Contains(menu))
            {
                tree.Menus.Add(menu);
            }

            if (parts.Length == 1)
            {
                continue;
            }

            string segment = parts[1].ToLowerInvariant();

            switch (segment)
            {
                case "view":
                    menu.CanView = true;
                    break;
                case "tab" when parts.Length >= 3:
                    string tabKey = parts[2];
                    TabPermission tab = menu.Tabs.FirstOrDefault(t => t.Key == tabKey) ?? new TabPermission { Key = tabKey };
                    if (!menu.Tabs.Contains(tab))
                    {
                        menu.Tabs.Add(tab);
                    }

                    if (parts.Length >= 4 && parts[3].Equals("view", StringComparison.InvariantCultureIgnoreCase))
                    {
                        tab.CanView = true;
                    }

                    break;
                case "field" when parts.Length >= 3:
                    string fieldKey = parts[2];
                    FieldPermission field = menu.Fields.FirstOrDefault(f => f.Key == fieldKey) ??
                                new FieldPermission { Key = fieldKey };
                    if (!menu.Fields.Contains(field))
                    {
                        menu.Fields.Add(field);
                    }

                    if (parts.Length >= 4)
                    {
                        string action = parts[3].ToLowerInvariant();
                        switch (action)
                        {
                            case "view":
                                field.CanView = true;
                                break;
                            case "edit":
                                field.CanEdit = true;
                                break;
                        }
                    }

                    break;
                default:
                    ActionPermission actionPerm = menu.Actions.FirstOrDefault(a => a.Key == segment) ??
                                     new ActionPermission { Key = segment };
                    if (!menu.Actions.Contains(actionPerm))
                    {
                        menu.Actions.Add(actionPerm);
                    }

                    actionPerm.CanExec = true;
                    break;
            }
        }

        return tree;
    }
}
