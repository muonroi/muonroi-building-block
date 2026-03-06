namespace Muonroi.AspNetCore.Exceptions;

public class PermissionDeniedException(string? message) : Exception(message);
