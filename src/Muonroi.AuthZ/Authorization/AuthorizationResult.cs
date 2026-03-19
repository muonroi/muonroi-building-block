namespace Muonroi.AuthZ.Authorization;

/// <summary>
/// Result returned by IAuthorizationPolicyEvaluator.
/// </summary>
public sealed class AuthorizationResult
{
    /// <summary>
    /// Gets a value indicating whether the authorization request was allowed.
    /// </summary>
    public bool IsAuthorized { get; private init; }

    /// <summary>
    /// Gets the denial reason when <see cref="IsAuthorized"/> is <see langword="false"/>.
    /// </summary>
    public string? DeniedReason { get; private init; }

    private AuthorizationResult() { }

    /// <summary>
    /// Creates a successful authorization result.
    /// </summary>
    public static AuthorizationResult Allow() => new() { IsAuthorized = true };

    /// <summary>
    /// Creates a failed authorization result with a reason.
    /// </summary>
    public static AuthorizationResult Deny(string reason) => new()
    {
        IsAuthorized = false,
        DeniedReason = reason
    };
}
