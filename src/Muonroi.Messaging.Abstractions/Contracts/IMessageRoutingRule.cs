using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Messaging.Abstractions.Contracts;

public interface IMessageRoutingRule<TMessage> : IRule<TMessage> where TMessage : class
{
    // Marker interface for routing rules
}
