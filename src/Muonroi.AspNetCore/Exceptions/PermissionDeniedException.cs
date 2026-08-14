namespace Muonroi.AspNetCore.Exceptions;

/// <inheritdoc />
public sealed class PermissionDeniedException : MException
{
    /// <summary>Initializes a new instance with a human-readable message.</summary>
    public PermissionDeniedException(string? message)
        : base("PERMISSION_DENIED", message ?? "Permission denied.", MExceptionCategory.Security, 403)
    {
    }
}
