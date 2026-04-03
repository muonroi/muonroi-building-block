namespace Muonroi.Mediator.Mediator.Interfaces;

/// <summary>
/// Builds mediator notifications from a rule context after a rule passes.
/// </summary>
/// <typeparam name="TContext">The rule context type used to create the notification.</typeparam>
public interface IRuleNotificationFactory<in TContext>
    where TContext : class
{
    /// <summary>
    /// Builds the notification that should be published for the provided <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The rule context that produced a passing result.</param>
    /// <returns>The notification to publish.</returns>
    INotification BuildNotification(TContext context);
}
