using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Messaging.Abstractions.Contracts;

/// <summary>
/// Represents a legacy routing rule that can approve or reject a message but cannot redirect it.
/// </summary>
/// <typeparam name="TMessage">The message type handled by the rule.</typeparam>
public interface IMessageRoutingRule<TMessage> : IRule<TMessage> where TMessage : class
{
    // Marker interface for legacy routing rules.
}
