namespace Muonroi.Mediator.Exceptions;

/// <summary>
/// Thrown when a request requires an authenticated tenant context but none is present.
/// </summary>
public sealed class MUnauthorizedException : Exception
{
    public MUnauthorizedException()
        : base("Request requires an authenticated tenant context.") { }

    public MUnauthorizedException(string message)
        : base(message) { }
}
