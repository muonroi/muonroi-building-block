namespace Muonroi.AspNetCore.Exceptions;

/// <inheritdoc />
public class InvalidPermissionException(string? message) : Exception(message)
{
}
