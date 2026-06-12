namespace Muonroi.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a message or request cannot be routed to its intended target.
/// Usually indicates a configuration error, missing registration, or invalid routing key.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RoutingRejectedException"/> class.
/// </remarks>
/// <param name="message">The human-readable error message explaining why routing was rejected.</param>
/// <param name="errorCode">The optional machine-readable error code (default: ROUTING_REJECTED).</param>
/// <param name="innerException">Optional inner exception that caused the routing failure.</param>
public sealed class RoutingRejectedException(string message, string? errorCode = null, Exception? innerException = null) 
    : MException(errorCode ?? "ROUTING_REJECTED", message, MExceptionCategory.Domain, 400, innerException)
{
}
