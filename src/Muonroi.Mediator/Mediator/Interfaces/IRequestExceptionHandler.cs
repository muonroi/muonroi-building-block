namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Holds the result state for an exception handler invocation.
/// </summary>
public sealed class RequestExceptionHandlerState<TResponse>
{
    /// <summary>Set to true inside the handler to mark the exception as handled.</summary>
    public bool Handled { get; private set; }

    /// <summary>The response to return when the exception is handled.</summary>
    public TResponse? Response { get; private set; }

    /// <summary>Marks the exception as handled and provides the response to return.</summary>
    public void SetHandled(TResponse response)
    {
        Handled = true;
        Response = response;
    }
}

/// <summary>
/// Defines an exception handler for a specific exception type during request processing.
/// Registered handlers are tried in DI registration order.
/// If <see cref="RequestExceptionHandlerState{TResponse}.Handled"/> is true after a handler runs,
/// subsequent handlers are skipped and the response is returned.
/// </summary>
public interface IRequestExceptionHandler<in TRequest, TResponse, in TException>
    where TException : Exception
{
    Task HandleAsync(
        TRequest request,
        TException exception,
        RequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken = default);
}
