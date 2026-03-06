namespace Muonroi.AspNetCore.Exceptions;

public class InvalidPermissionException(string? message) : Exception(message)
{
}
