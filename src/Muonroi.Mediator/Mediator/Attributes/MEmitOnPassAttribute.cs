using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Mediator.Mediator.Attributes;

/// <summary>
/// Declares a notification type that should be published when the annotated rule passes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class MEmitOnPassAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MEmitOnPassAttribute"/> class.
    /// </summary>
    /// <param name="notificationType">The notification type to publish when the rule passes.</param>
    public MEmitOnPassAttribute(Type notificationType)
    {
        NotificationType = MGuard.NotNull(notificationType);
    }

    /// <summary>
    /// Gets the notification type to publish when the rule passes.
    /// </summary>
    public Type NotificationType { get; }
}
