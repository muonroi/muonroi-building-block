using Muonroi.Mediator.Mediator.Interfaces;

namespace TestProject.Service.Core.Contracts;

/// <summary>Command for handler dispatch testing via MSiteCommandHandler keyed DI pattern.</summary>
public sealed record CreateOrderCommand(string Name, string Description) : IRequest<CreateOrderResponse>;

/// <summary>Response returned by a per-site CreateOrderCommand handler.</summary>
/// <param name="Success">Whether the command executed successfully.</param>
/// <param name="Message">A site-specific message containing the handler marker (e.g. "ALPHA-Handled").</param>
/// <param name="HandlerType">The concrete handler type name that processed this command.</param>
public sealed record CreateOrderResponse(bool Success, string Message, string HandlerType);
