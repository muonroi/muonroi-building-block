

namespace Muonroi.RuleEngine.Core.Runtime;

/// <summary>
/// Represents a ready-to-fire rule with metadata.
/// </summary>
public sealed class Activation(Func<IRuleContext, Task> action, int priority = 0, string? group = null)
{

    /// <summary>Gets the rule action to execute.</summary>
    public Func<IRuleContext, Task> Action { get; } = action ?? throw new ArgumentNullException(nameof(action));

    /// <summary>Priority of this activation. Higher values fire first.</summary>
    public int Priority { get; } = priority;

    /// <summary>Group name used for halting related activations.</summary>
    public string? Group { get; } = group;
}
