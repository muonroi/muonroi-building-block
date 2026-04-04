namespace Muonroi.Governance.Authorization;

/// <summary>
/// Represents the MPolicy Decision Request.
/// </summary>
public sealed class MPolicyDecisionRequest
{
    /// <summary>
    /// Gets or sets the Decision Type.
    /// </summary>
    public string DecisionType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the User Id.
    /// </summary>
    public string? UserId { get; set; }
    /// <summary>
    /// Gets or sets the Tenant Id.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// Gets or sets the Correlation Id.
    /// </summary>
    public string? CorrelationId { get; set; }
    /// <summary>
    /// Gets or sets the Action.
    /// </summary>
    public string? Action { get; set; }
    /// <summary>
    /// Gets or sets the Resource.
    /// </summary>
    public string? Resource { get; set; }
    /// <summary>
    /// Gets or sets the Required Any Permissions.
    /// </summary>
    public string[] RequiredAnyPermissions { get; set; } = [];
    /// <summary>
    /// Gets or sets the Required All Permissions.
    /// </summary>
    public string[] RequiredAllPermissions { get; set; } = [];
    /// <summary>
    /// Gets or sets the User Permissions Bitmask.
    /// </summary>
    public long? UserPermissionsBitmask { get; set; }
    /// <summary>
    /// Gets or sets the User Permissions.
    /// </summary>
    public string[] UserPermissions { get; set; } = [];
    /// <summary>
    /// Executes the Claims operation.
    /// </summary>
    public Dictionary<string, string> Claims { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Represents the MPolicy Decision Result.
/// </summary>
public sealed class MPolicyDecisionResult
{
    /// <summary>
    /// Gets or sets the Is Allowed.
    /// </summary>
    public bool IsAllowed { get; init; }
    /// <summary>
    /// Gets or sets the Is Authoritative.
    /// </summary>
    public bool IsAuthoritative { get; init; }
    /// <summary>
    /// Gets or sets the Used Fallback.
    /// </summary>
    public bool UsedFallback { get; init; }
    /// <summary>
    /// Gets or sets the Decision Source.
    /// </summary>
    public string DecisionSource { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the Error.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Executes the Allowed operation.
    /// </summary>
    public static MPolicyDecisionResult Allowed(string source)
    {
        return new()
        {
            IsAllowed = true,
            IsAuthoritative = true,
            DecisionSource = source
        };
    }

    /// <summary>
    /// Executes the Denied operation.
    /// </summary>
    public static MPolicyDecisionResult Denied(string source, string? error = null)
    {
        return new()
        {
            IsAllowed = false,
            IsAuthoritative = true,
            DecisionSource = source,
            Error = error
        };
    }

    /// <summary>
    /// Executes the Local Fallback operation.
    /// </summary>
    public static MPolicyDecisionResult LocalFallback(string source, string? error = null)
    {
        return new()
        {
            IsAllowed = false,
            IsAuthoritative = false,
            UsedFallback = true,
            DecisionSource = source,
            Error = error
        };
    }
}
