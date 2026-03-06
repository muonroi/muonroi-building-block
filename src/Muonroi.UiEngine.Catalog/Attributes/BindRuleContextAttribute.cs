namespace Muonroi.UiEngine.Catalog.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class BindRuleContextAttribute(Type contextType) : Attribute
{
    public Type ContextType { get; } = contextType ?? throw new ArgumentNullException(nameof(contextType));

    /// <summary>
    /// Optional runtime workflow name bound to this endpoint.
    /// </summary>
    public string? WorkflowName { get; init; }
}
