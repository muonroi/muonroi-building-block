namespace Muonroi.Mediator.Exceptions;

/// <summary>
/// Thrown when the current user lacks the required roles or permissions to execute a request.
/// </summary>
public sealed class MForbiddenException : MException
{
    /// <summary>
    /// Gets the Required Roles.
    /// </summary>
    public IReadOnlyList<string> RequiredRoles { get; }
    /// <summary>
    /// Gets the Required Permissions.
    /// </summary>
    public IReadOnlyList<string> RequiredPermissions { get; }

    /// <summary>
    /// Initializes a new instance of MForbiddenException.
    /// </summary>
    public MForbiddenException(IReadOnlyList<string> requiredRoles, IReadOnlyList<string> requiredPermissions)
        : base("FORBIDDEN", BuildMessage(requiredRoles, requiredPermissions), MExceptionCategory.Security, 403)
    {
        RequiredRoles = requiredRoles;
        RequiredPermissions = requiredPermissions;
        Details["RequiredRoles"] = requiredRoles;
        Details["RequiredPermissions"] = requiredPermissions;
    }

    private static string BuildMessage(IReadOnlyList<string> roles, IReadOnlyList<string> permissions)
    {
        List<string> parts = [];
        if (roles.Count > 0)
            parts.Add($"roles: [{string.Join(", ", roles)}]");
        if (permissions.Count > 0)
            parts.Add($"permissions: [{string.Join(", ", permissions)}]");
        return $"Access denied. Missing {string.Join(" and ", parts)}.";
    }
}
