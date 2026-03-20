using Muonroi.Mediator.Mediator;

namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Represents the IRequest Handler{TRequest, MResponse}.
/// </summary>
public interface IRequestHandler<TRequest, MResponse> where TRequest : IRequest<MResponse>
{
    /// <summary>
    /// Executes the Handle operation.
    /// </summary>
    Task<MResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Represents the IRequest Handler{TRequest}.
/// </summary>
public interface IRequestHandler<TRequest> : IRequestHandler<TRequest, Unit> where TRequest : IRequest<Unit>
{
}
