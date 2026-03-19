namespace Muonroi.Mediator.Exceptions;

/// <summary>
/// Thrown when the current user lacks the required roles or permissions to execute a request.
/// </summary>
public sealed class MForbiddenException : Exception
{
    public IReadOnlyList<string> RequiredRoles { get; }
    public IReadOnlyList<string> RequiredPermissions { get; }

    public MForbiddenException(IReadOnlyList<string> requiredRoles, IReadOnlyList<string> requiredPermissions)
        : base(BuildMessage(requiredRoles, requiredPermissions))
    {
        RequiredRoles = requiredRoles;
        RequiredPermissions = requiredPermissions;
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
