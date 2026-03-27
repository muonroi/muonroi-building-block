using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models;
using Muonroi.Tenancy.Core;

namespace Muonroi.AspNetCore.Controllers;

/// <inheritdoc />
public abstract class UiEngineRuntimeManifestControllerBase(
    IEnumerable<IUiEngineManifestContributor> contributors,
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    IMDateTimeService dateTimeService,
    IMJsonSerializeService jsonSerializeService,
    IMControllerExecutionContextResolver? executionContextResolver = null) : ControllerBase
{
/// <inheritdoc />
    [HttpGet]
    public virtual IActionResult GetRoot()
    {
        string prefix = ResolveRoutePrefix();
        return Ok(new
        {
            current = $"{prefix}/current",
            contractInfo = $"{prefix}/contract-info",
            schemaHash = $"{prefix}/schema-hash",
            userTemplate = $"{prefix}/{{userId}}"
        });
    }

/// <inheritdoc />
    [HttpGet("current")]
    public virtual async Task<IActionResult> GetCurrent(
        [FromQuery] string? minimalFor,
        CancellationToken cancellationToken = default)
    {
        MControllerExecutionContext context = ResolveExecutionContext();
        Guid userId = ResolveEffectiveUserId(Guid.Empty, context);
        MUiEngineManifest manifest = await BuildManifestAsync(userId, context, cancellationToken);
        manifest = BuildProjectedManifest(manifest, minimalFor);
        return BuildManifestWithEtagResult(manifest);
    }

/// <inheritdoc />
    [HttpGet("{userId:guid}")]
    public virtual async Task<IActionResult> GetByUser(
        Guid userId,
        [FromQuery] string? minimalFor,
        CancellationToken cancellationToken = default)
    {
        MControllerExecutionContext context = ResolveExecutionContext();
        MUiEngineManifest manifest = await BuildManifestAsync(userId, context, cancellationToken);
        manifest = BuildProjectedManifest(manifest, minimalFor);
        return BuildManifestWithEtagResult(manifest);
    }

/// <inheritdoc />
    [HttpGet("contract-info")]
    public virtual IActionResult GetContractInfo()
    {
        string prefix = ResolveRoutePrefix();
        return Ok(new MUiEngineContractInfo
        {
            CurrentManifestEndpoint = $"{prefix}/current",
            UserManifestEndpointTemplate = $"{prefix}/{{userId}}",
            SchemaHashEndpoint = $"{prefix}/schema-hash",
            NotifyChangeEndpoint = $"{prefix}/notify-change"
        });
    }

/// <inheritdoc />
    [HttpGet("schema-hash")]
    public virtual async Task<IActionResult> GetSchemaHash(CancellationToken cancellationToken = default)
    {
        MControllerExecutionContext context = ResolveExecutionContext();
        Guid userId = ResolveEffectiveUserId(Guid.Empty, context);
        MUiEngineManifest manifest = await BuildManifestAsync(userId, context, cancellationToken);
        string payload = jsonSerializeService.Serialize(manifest);

        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return Ok(new MUiEngineSchemaVersion
        {
            Version = manifest.SchemaVersion,
            SchemaHash = hash,
            OpenApiHash = hash,
            GeneratedAtUtc = dateTimeService.UtcNow()
        });
    }

    /// <summary>
    /// Gets the route prefixes used to resolve UI engine endpoints.
    /// </summary>
    protected virtual IReadOnlyList<string> RoutePrefixes =>
    [
        "/api/v1/ui-engine",
        "/api/v1/auth/ui-engine",
        "/api/v1/ui-engine-lab/ui-engine"
    ];

    /// <summary>
    /// Resolves the tenant tier for UI engine manifest generation.
    /// </summary>
    protected virtual string ResolveTenantTier()
    {
        return configuration.GetValue<string>("UiEngineLab:TenantTier") ?? "Free";
    }

    /// <summary>
    /// Resolves the tenant identifier for UI engine manifest generation.
    /// </summary>
    protected virtual string ResolveTenantId()
    {
        return configuration.GetValue<string>("UiEngineLab:TenantId") ?? "_global";
    }

    /// <summary>
    /// Resolves the current user's permission set.
    /// </summary>
    protected virtual IReadOnlyList<string> ResolveUserPermissions()
    {
        return [];
    }

    /// <summary>
    /// Resolves the active API route prefix for the current request.
    /// </summary>
    protected virtual string ResolveRoutePrefix()
    {
        string path = Request.Path.Value ?? string.Empty;
        foreach (string prefix in RoutePrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return prefix;
            }
        }

        return RoutePrefixes[0];
    }

    /// <summary>
    /// Builds the UI engine manifest for a user using the current execution context.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected virtual async Task<MUiEngineManifest> BuildManifestAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await BuildManifestAsync(userId, ResolveExecutionContext(), cancellationToken);
    }

    /// <summary>
    /// Builds the UI engine manifest for a specific user.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="executionContext">Execution context overrides.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    protected virtual async Task<MUiEngineManifest> BuildManifestAsync(
        Guid userId,
        MControllerExecutionContext? executionContext,
        CancellationToken cancellationToken)
    {
        Guid effectiveUserId = ResolveEffectiveUserId(userId, executionContext);
        string tenantTier = ResolveTenantTier(executionContext);
        string tenantId = ResolveTenantId(executionContext);
        IReadOnlyList<string> permissions = ResolveUserPermissions(executionContext);

        MUiEngineManifest manifest = new()
        {
            UserId = effectiveUserId,
            TenantId = tenantId,
            LicenseTier = tenantTier
        };

        UiEngineManifestContext context = new()
        {
            Manifest = manifest,
            TenantTier = tenantTier,
            TenantId = tenantId,
            UserId = effectiveUserId,
            UserPermissions = [.. permissions],
            Services = serviceProvider
        };

        foreach (IUiEngineManifestContributor contributor in contributors.OrderBy(x => x.Order))
        {
            await contributor.ContributeAsync(context, cancellationToken);
        }

        return manifest;
    }

    /// <summary>
    /// Resolves the tenant tier from an execution context override.
    /// </summary>
    protected virtual string ResolveTenantTier(MControllerExecutionContext? executionContext)
    {
        if (!string.IsNullOrWhiteSpace(executionContext?.TenantTier))
        {
            return executionContext.TenantTier;
        }

        return ResolveTenantTier();
    }

    /// <summary>
    /// Resolves the tenant identifier from an execution context override.
    /// </summary>
    protected virtual string ResolveTenantId(MControllerExecutionContext? executionContext)
    {
        if (!string.IsNullOrWhiteSpace(executionContext?.TenantId))
        {
            return executionContext.TenantId;
        }

        return ResolveTenantId();
    }

    /// <summary>
    /// Resolves the permissions list from an execution context override.
    /// </summary>
    protected virtual IReadOnlyList<string> ResolveUserPermissions(MControllerExecutionContext? executionContext)
    {
        if (executionContext is { Permissions.Count: > 0 })
        {
            return executionContext.Permissions;
        }

        return ResolveUserPermissions();
    }

    /// <summary>
    /// Resolves the execution context for the current request.
    /// </summary>
    protected virtual MControllerExecutionContext ResolveExecutionContext()
    {
        HttpContext? httpContext = HttpContext;
        if (httpContext is null)
        {
            return new MControllerExecutionContext();
        }

        IMControllerExecutionContextResolver? resolver = executionContextResolver
            ?? httpContext.RequestServices.GetService(typeof(IMControllerExecutionContextResolver))
                as IMControllerExecutionContextResolver;

        return resolver?.Resolve(httpContext) ?? BuildFallbackExecutionContext(httpContext);
    }

    private MControllerExecutionContext BuildFallbackExecutionContext(HttpContext httpContext)
    {
        IAuthenticateInfoContext? authContext =
            httpContext.RequestServices.GetService(typeof(IAuthenticateInfoContext)) as IAuthenticateInfoContext;

        ClaimsPrincipal user = httpContext.User;
        string? tenantId = authContext?.TenantId
                           ?? user.FindFirst(ClaimConstants.TenantId)?.Value
                           ?? ReadHeader(httpContext, "X-Tenant-Id")
                           ?? ReadHeader(httpContext, "TenantId")
                           ?? TenantContext.CurrentTenantId
                           ?? configuration.GetValue<string>("UiEngineLab:TenantId");

        string? username = authContext?.CurrentUsername
                           ?? user.FindFirst(ClaimConstants.Username)?.Value
                           ?? user.Identity?.Name
                           ?? ReadHeader(httpContext, "X-Username");

        string? rawUserId = authContext?.CurrentUserGuid
                            ?? user.FindFirst(ClaimConstants.UserIdentifier)?.Value
                            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? ReadHeader(httpContext, "X-User-Id");

        string? rawPermissions = authContext?.Permission
                                 ?? user.FindFirst(ClaimConstants.Permission)?.Value
                                 ?? ReadHeader(httpContext, "X-Permissions");

        return new MControllerExecutionContext
        {
            UserId = ParseGuid(rawUserId),
            Username = username,
            TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim(),
            TenantTier = ReadHeader(httpContext, "X-Tenant-Tier") ?? configuration.GetValue<string>("UiEngineLab:TenantTier"),
            Actor = ReadHeader(httpContext, "X-Actor") ?? username,
            IsAuthenticated = authContext?.IsAuthenticated == true || user.Identity?.IsAuthenticated == true,
            Permissions = ParsePermissions(rawPermissions)
        };
    }

    private static Guid ResolveEffectiveUserId(Guid requestedUserId, MControllerExecutionContext? executionContext)
    {
        if (requestedUserId != Guid.Empty)
        {
            return requestedUserId;
        }

        return executionContext?.UserId ?? Guid.Empty;
    }

    private static Guid? ParseGuid(string? value)
    {
        return Guid.TryParse(value, out Guid parsed) ? parsed : null;
    }

    private static IReadOnlyList<string> ParsePermissions(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return [.. value
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string? ReadHeader(HttpContext httpContext, string key)
    {
        if (!httpContext.Request.Headers.TryGetValue(key, out Microsoft.Extensions.Primitives.StringValues values))
        {
            return null;
        }

        string? value = values.FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private IActionResult BuildManifestWithEtagResult(MUiEngineManifest manifest)
    {
        string etag = BuildManifestEtag(manifest);
        Response.Headers.ETag = etag;
        string requestEtag = Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrWhiteSpace(requestEtag) &&
            string.Equals(requestEtag.Trim(), etag, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(manifest);
    }

    private static string BuildManifestEtag(MUiEngineManifest manifest)
    {
        MUiEngineManifest normalized = CloneForEtag(manifest);
        string payload = JsonSerializer.Serialize(normalized, new JsonSerializerOptions // MBB002-exempt: static helper with custom options not available in wrapper
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
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
}
