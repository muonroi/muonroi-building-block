using Muonroi.Mediator.Mediator;

namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Represents the IRequest{MResponse}.
/// </summary>
public interface IRequest<out MResponse> { }
/// <summary>
/// Represents the IRequest.
/// </summary>
public interface IRequest : IRequest<Unit> { }
