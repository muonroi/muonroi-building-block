namespace Muonroi.Mediator.Exceptions;

/// <summary>
/// Thrown when a request requires an authenticated tenant context but none is present.
/// </summary>
public sealed class MUnauthorizedException : Exception
{
    /// <summary>
    /// Initializes a new instance of MUnauthorizedException.
    /// </summary>
    public MUnauthorizedException()
        : base("Request requires an authenticated tenant context.") { }

    /// <summary>
    /// Initializes a new instance of MUnauthorizedException.
    /// </summary>
    public MUnauthorizedException(string message)
        : base(message) { }
}
