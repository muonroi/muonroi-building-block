namespace Muonroi.AspNetCore.Exceptions;

/// <inheritdoc />
public class PermissionDeniedException(string? message) : Exception(message);
