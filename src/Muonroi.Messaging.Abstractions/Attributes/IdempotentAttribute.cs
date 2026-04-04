namespace Muonroi.Messaging.Abstractions.Attributes;

/// <summary>
/// Marks a consumer as requiring idempotent processing via the message inbox.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class IdempotentAttribute : Attribute
{
}
