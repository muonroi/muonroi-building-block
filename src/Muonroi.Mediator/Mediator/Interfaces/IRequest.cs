using Muonroi.Mediator.Mediator;

namespace Muonroi.Mediator.Mediator.Interfaces;

public interface IRequest<out MResponse> { }
public interface IRequest : IRequest<Unit> { }
