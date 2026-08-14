namespace Muonroi.UiEngine.Catalog.Attributes;

/// <summary>
/// Binds a rule context type to an endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class BindRuleContextAttribute(Type contextType) : Attribute
{
    /// <summary>
    /// The bound rule context type.
    /// </summary>
    public Type ContextType { get; } = MGuard.NotNull(contextType);

    /// <summary>
    /// Optional runtime workflow name bound to this endpoint.
    /// </summary>
    public string? WorkflowName { get; init; }
}
