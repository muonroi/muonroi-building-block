using System.Text.Json;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions.Adapters;
using Muonroi.RuleEngine.Runtime.Compilation.Feel;
using Muonroi.RuleEngine.DecisionTable.Feel;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Executes a FEEL boolean expression as an <see cref="IRule{TContext}"/>.
/// Evaluates to <see cref="RuleResult.Passed()"/> when the expression is true;
/// <see cref="RuleResult.Failure(string[])"/> otherwise.
/// </summary>
/// <typeparam name="TContext">The rule execution context type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="FeelRuleAdapter{TContext}"/> class.
/// </remarks>
/// <param name="code">The rule code.</param>
/// <param name="expression">The FEEL expression.</param>
/// <param name="outputFields">The output fields written on pass.</param>
/// <param name="projector">The context projector used to build FEEL variables.</param>
/// <param name="log">The adapter logger.</param>
public sealed class FeelRuleAdapter<TContext>(
    string code,
    string expression,
    IReadOnlyList<FeelOutputField>? outputFields,
    IContextProjector<TContext> projector,
    IMLog<FeelRuleAdapter<TContext>> log) : IRule<TContext>
{
    private readonly string _code = code;
    private readonly string _expression = expression;
    private readonly IReadOnlyList<FeelOutputField> _outputFields = outputFields ?? [];
    private readonly IContextProjector<TContext> _projector = projector;
    private readonly IMLog<FeelRuleAdapter<TContext>> _log = log;
    private Func<IDictionary<string, object>, bool>? _compiledDelegate;

    /// <summary>
    /// Gets the unique rule code.
    /// </summary>
    public string Code => _code;

    /// <summary>
    /// Gets or sets the execution order.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets or sets the dependent rule codes.
    /// </summary>
    public string[] DependsOn { get; init; } = [];

    /// <summary>
    /// Gets the hook point for the adapter.
    /// </summary>
    public HookPoint HookPoint => HookPoint.BeforeRule;

    /// <summary>
    /// Gets the rule type.
    /// </summary>
    public RuleType Type => RuleType.Validation;

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string Name => $"FEEL:{_code}";

    /// <summary>
    /// Gets the dependent service types.
    /// </summary>
    public IEnumerable<Type> Dependencies => [];

    /// <summary>
    /// Evaluates the FEEL condition and writes configured output fields on success.
    /// </summary>
    /// <param name="ctx">The execution context.</param>
    /// <param name="facts">The fact bag.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The rule result.</returns>
    public Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_expression))
        {
            return Task.FromResult(
                RuleResult.Failure($"FEEL expression in '{_code}' is empty."));
        }

        Dictionary<string, object> feelVars = BuildFeelVariables(ctx, facts);

        bool passed;
        try
        {
            _compiledDelegate ??= FeelExpressionCompiler.Compile(_expression);
            passed = _compiledDelegate(feelVars);
        }
        catch (Exception ex)
        {
            _log.Warn("FEEL expression '{Code}' threw: {Message}", _code, ex.Message);
            return Task.FromResult(
                RuleResult.Failure($"FEEL expression error in '{_code}': {ex.Message}"));
        }

        RuleResult result = passed
            ? RuleResult.Passed()
            : RuleResult.Failure($"Condition '{_code}' evaluated to false");

        if (passed)
        {
            foreach (FeelOutputField outputField in _outputFields)
            {
                object? value = FeelEvaluator.EvaluateValue(outputField.ValueExpression, feelVars);
                facts.Set(outputField.Path, value);
                facts.Set($"__node.{_code}.{outputField.Path}", value);
                if (value is not null)
                {
                    feelVars[outputField.Path] = value;
                }
            }
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Executes no side effects because FEEL adapters only evaluate conditions.
    /// </summary>
    /// <param name="context">The execution context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completed task.</returns>
    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private Dictionary<string, object> BuildFeelVariables(TContext ctx, FactBag facts)
    {
        Dictionary<string, object> vars = new(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, object?> kv in _projector.Project(ctx))
        {
            object? unwrapped = UnwrapJsonElement(kv.Value);
            if (unwrapped is not null)
            {
                vars[kv.Key] = unwrapped;
            }
        }

        foreach (string key in facts.Keys)
        {
            object? unwrapped = UnwrapJsonElement(facts.Get<object>(key));
            if (unwrapped is not null)
            {
                vars[key] = unwrapped;
            }
        }

        return vars;
    }

    /// <summary>
    /// Recursively converts <see cref="JsonElement"/> values (and nested objects/arrays) into native
    /// CLR types so FEEL path navigation, boolean coercion, and comparison work uniformly.
    ///
    /// <para>
    /// When the execution context is a <c>Dictionary&lt;string, object?&gt;</c> (the dry-run / dynamic
    /// flow-graph path), <c>System.Text.Json</c> deserializes every value as a <see cref="JsonElement"/>.
    /// A nested object such as <c>vgm</c> therefore arrives as a <c>JsonElement</c> (ValueKind.Object),
    /// not a dictionary — so <c>vgm.isVgm</c> could not be navigated (reflection found no CLR property
    /// and returned <c>null</c>). A nested number masked the defect because <c>Compare(null, x)</c>
    /// treats null as less-than (so <c>vgm.wgt &lt;= maxGross</c> was true regardless of the value),
    /// while a nested boolean exposed it (<c>vgm.isVgm = true</c> compared null against true → false).
    /// Unwrapping to <c>Dictionary&lt;string, object?&gt;</c> / native scalars restores correct nested
    /// access. Already-native values pass through unchanged (no-op for typed POCO contexts).
    /// </para>
    /// </summary>
    private static object? UnwrapJsonElement(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out int i) => i,
            JsonValueKind.Number when element.TryGetInt64(out long l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out decimal dec) => dec,
            JsonValueKind.Number when element.TryGetDouble(out double d) => d,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(e => UnwrapJsonElement(e))
                .ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    p => p.Name,
                    p => UnwrapJsonElement(p.Value),
                    StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }
}
