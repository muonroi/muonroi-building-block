using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Mediator.Mediator.Attributes;

/// <summary>
/// Declares a notification type that should be published when the annotated rule passes.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MEmitOnPassAttribute"/> class.
/// </remarks>
/// <param name="notificationType">The notification type to publish when the rule passes.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MEmitOnPassAttribute(Type notificationType) : Attribute
{

    /// <summary>
    /// Gets the notification type to publish when the rule passes.
    /// </summary>
    public Type NotificationType { get; } = MGuard.NotNull(notificationType);
}
