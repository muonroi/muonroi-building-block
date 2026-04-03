namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Abstract base class for rules that need to read from or write to the <see cref="FactBag"/>
/// shared across nodes in a flow graph. Provides convenient accessor helpers so compiled
/// rules can consume output fields produced by upstream FEEL/Liquid/DecisionTable nodes.
/// </summary>
/// <typeparam name="TContext">The rule execution context type.</typeparam>
public abstract class MFactBagAwareRule<TContext> : IRule<TContext>
{
    /// <summary>
    /// Gets the unique rule code.
    /// </summary>
    public abstract string Code { get; }

    /// <summary>
    /// Gets the execution order.
    /// </summary>
    public virtual int Order => 0;

    /// <summary>
    /// Gets the codes of rules that must run before this rule.
    /// </summary>
    public virtual IReadOnlyList<string> DependsOn => [];

    /// <summary>
    /// Gets the hook point where this rule executes.
    /// </summary>
    public virtual HookPoint HookPoint => HookPoint.BeforeRule;

    /// <summary>
    /// Gets the rule type.
    /// </summary>
    public virtual RuleType Type => RuleType.Validation;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public virtual string Name => GetType().Name;

    /// <summary>
    /// Gets the dependent service types.
    /// </summary>
    public virtual IEnumerable<Type> Dependencies => [];

    /// <summary>
    /// The upstream <see cref="FactBag"/> from prior nodes.
    /// Available after <see cref="EvaluateAsync"/> is called.
    /// </summary>
    protected FactBag Facts { get; private set; } = new();

    /// <summary>
    /// Reads a fact from upstream nodes by path.
    /// </summary>
    /// <typeparam name="T">The expected type of the fact value.</typeparam>
    /// <param name="path">The fact key path (e.g. "gate.result").</param>
    /// <returns>The fact value if found and castable; otherwise <c>default</c>.</returns>
    protected T? ReadFact<T>(string path) => Facts.Get<T>(path);

    /// <summary>
    /// Writes a fact for downstream nodes to consume.
    /// </summary>
    /// <typeparam name="T">The type of the fact value.</typeparam>
    /// <param name="path">The fact key path.</param>
    /// <param name="value">The value to store.</param>
    protected void WriteFact<T>(string path, T value) => Facts.Set(path, value);

    /// <summary>
    /// Checks whether a prior node was executed and passed.
    /// </summary>
    /// <param name="nodeId">The node identifier from the flow graph.</param>
    /// <returns><c>true</c> if the node was executed and its result was a pass.</returns>
    protected bool NodePassed(string nodeId)
        => Facts.Get<bool?>($"__graph.node.{nodeId}.passed") == true;

    /// <summary>
    /// Checks whether a prior node was executed (regardless of pass/fail).
    /// </summary>
    /// <param name="nodeId">The node identifier from the flow graph.</param>
    /// <returns><c>true</c> if the node was executed.</returns>
    protected bool NodeExecuted(string nodeId)
        => Facts.Get<bool?>($"__graph.node.{nodeId}.executed") == true;

    /// <summary>
    /// Gets the result payload of a specific node by its identifier.
    /// </summary>
    /// <typeparam name="T">The expected type of the result value.</typeparam>
    /// <param name="nodeId">The node identifier from the flow graph.</param>
    /// <returns>The node-scoped result if found; otherwise <c>default</c>.</returns>
    protected T? NodeResult<T>(string nodeId)
        => Facts.Get<T>($"__graph.node.{nodeId}.result");

    /// <summary>
    /// Captures the <see cref="FactBag"/> and delegates to <see cref="EvaluateCoreAsync"/>.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <param name="facts">The shared fact bag.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The rule result.</returns>
    public Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        Facts = facts;
        return EvaluateCoreAsync(ctx, ct);
    }

    /// <summary>
    /// Override this to implement rule evaluation logic.
    /// Use <see cref="ReadFact{T}"/>, <see cref="WriteFact{T}"/>,
    /// <see cref="NodePassed"/>, and <see cref="NodeExecuted"/> to interact with the <see cref="FactBag"/>.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The rule result.</returns>
    protected abstract Task<RuleResult> EvaluateCoreAsync(TContext ctx, CancellationToken ct);

    /// <summary>
    /// Executes no side effects by default. Override to add post-evaluation behavior.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completed task.</returns>
    public virtual Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
