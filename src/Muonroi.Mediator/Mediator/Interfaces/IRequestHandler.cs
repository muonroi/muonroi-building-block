using Muonroi.Mediator.Mediator;

namespace Muonroi.Mediator.Mediator.Interfaces;

public interface IRequestHandler<TRequest, MResponse> where TRequest : IRequest<MResponse>
{
    Task<MResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public interface IRequestHandler<TRequest> : IRequestHandler<TRequest, Unit> where TRequest : IRequest<Unit>
{
}
