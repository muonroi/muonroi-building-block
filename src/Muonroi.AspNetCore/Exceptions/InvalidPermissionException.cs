namespace Muonroi.AspNetCore.Exceptions;

/// <inheritdoc />
public sealed class InvalidPermissionException : MException
{
    /// <summary>Initializes a new instance with a human-readable message.</summary>
    public InvalidPermissionException(string? message)
        : base("INVALID_PERMISSION", message ?? "Invalid permission.", MExceptionCategory.Security, 403)
    {
    }
}
