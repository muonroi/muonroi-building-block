namespace Muonroi.Governance.Authorization;

public sealed class MPolicyDecisionRequest
{
    public string DecisionType { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? CorrelationId { get; set; }
    public string? Action { get; set; }
    public string? Resource { get; set; }
    public string[] RequiredAnyPermissions { get; set; } = [];
    public string[] RequiredAllPermissions { get; set; } = [];
    public long? UserPermissionsBitmask { get; set; }
    public string[] UserPermissions { get; set; } = [];
    public Dictionary<string, string> Claims { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MPolicyDecisionResult
{
    public bool IsAllowed { get; init; }
    public bool IsAuthoritative { get; init; }
    public bool UsedFallback { get; init; }
    public string DecisionSource { get; init; } = string.Empty;
    public string? Error { get; init; }

    public static MPolicyDecisionResult Allowed(string source)
    {
        return new()
        {
            IsAllowed = true,
            IsAuthoritative = true,
            DecisionSource = source
        };
    }

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
