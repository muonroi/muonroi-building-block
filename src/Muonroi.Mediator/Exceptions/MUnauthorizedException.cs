namespace Muonroi.Mediator.Exceptions;

/// <summary>
/// Thrown when a request requires an authenticated tenant context but none is present.
/// </summary>
public sealed class MUnauthorizedException : MException
{
    /// <summary>
    /// Initializes a new instance of MUnauthorizedException.
    /// </summary>
    public MUnauthorizedException()
        : base("UNAUTHORIZED", "Request requires an authenticated tenant context.", MExceptionCategory.Security, 401) { }

    /// <summary>
    /// Initializes a new instance of MUnauthorizedException.
    /// </summary>
    public MUnauthorizedException(string message)
        : base("UNAUTHORIZED", message, MExceptionCategory.Security, 401) { }
}
