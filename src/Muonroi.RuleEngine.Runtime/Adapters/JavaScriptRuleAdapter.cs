using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions.Adapters;
using Muonroi.RuleEngine.Runtime.Compilation.JavaScript;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Executes a JavaScript expression as an <see cref="IRule{TContext}"/>.
/// Uses Jint (sandboxed .NET JavaScript interpreter).
/// Evaluates to <see cref="RuleResult.Passed()"/> when the expression is truthy;
/// <see cref="RuleResult.Failure(string[])"/> otherwise.
/// </summary>
/// <typeparam name="TContext">The rule execution context type.</typeparam>
public sealed class JavaScriptRuleAdapter<TContext> : IRule<TContext>
{
    private readonly string _code;
    private readonly string _expression;
    private readonly IReadOnlyList<FeelOutputField> _outputFields;
    private readonly IContextProjector<TContext> _projector;
    private readonly IMLog<JavaScriptRuleAdapter<TContext>> _log;
    private Func<IDictionary<string, object?>, object?>? _compiledDelegate;

    public string Code => _code;
    public int Order { get; init; }
    public string[] DependsOn { get; init; } = [];
    public HookPoint HookPoint => HookPoint.BeforeRule;
    public RuleType Type => RuleType.Validation;
    public string Name => $"JS:{_code}";
    public IEnumerable<Type> Dependencies => [];

    public JavaScriptRuleAdapter(
        string code,
        string expression,
        IReadOnlyList<FeelOutputField>? outputFields,
        IContextProjector<TContext> projector,
        IMLog<JavaScriptRuleAdapter<TContext>> log)
    {
        _code = code;
        _expression = expression;
        _outputFields = outputFields ?? [];
        _projector = projector;
        _log = log;
    }

    public Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_expression))
        {
            return Task.FromResult(
                RuleResult.Failure($"JavaScript expression in '{_code}' is empty."));
        }

        Dictionary<string, object?> jsVars = BuildVariables(ctx, facts);

        object? result;
        try
        {
            _compiledDelegate ??= JintExpressionCompiler.Compile(_expression);
            result = _compiledDelegate(jsVars);
        }
        catch (Exception ex)
        {
            _log.Warn("JavaScript expression '{Code}' threw: {Message}", _code, ex.Message);
            return Task.FromResult(
                RuleResult.Failure($"JavaScript expression error in '{_code}': {ex.Message}"));
        }

        bool passed = IsTruthy(result);
        RuleResult ruleResult = passed
            ? RuleResult.Passed()
            : RuleResult.Failure($"Condition '{_code}' evaluated to falsy");

        if (passed)
        {
            WriteOutputFields(jsVars, facts);
        }

        return Task.FromResult(ruleResult);
    }

    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private void WriteOutputFields(Dictionary<string, object?> jsVars, FactBag facts)
    {
        foreach (FeelOutputField outputField in _outputFields)
        {
            object? value;
            try
            {
                Func<IDictionary<string, object?>, object?> valueDelegate =
                    JintExpressionCompiler.Compile(outputField.ValueExpression);
                value = valueDelegate(jsVars);
            }
            catch
            {
                value = null;
            }

            facts.Set(outputField.Path, value);
            facts.Set($"__node.{_code}.{outputField.Path}", value);
            if (value is not null)
            {
                jsVars[outputField.Path] = value;
            }
        }
    }

    private Dictionary<string, object?> BuildVariables(TContext ctx, FactBag facts)
    {
        Dictionary<string, object?> vars = new(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, object?> kv in _projector.Project(ctx))
        {
            vars[kv.Key] = kv.Value;
        }

        foreach (string key in facts.Keys)
        {
            vars[key] = facts.Get<object>(key);
        }

        return vars;
    }

    private static bool IsTruthy(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            double d => Math.Abs(d) > double.Epsilon,
            int i => i != 0,
            long l => l != 0,
            string s => !string.IsNullOrEmpty(s),
            _ => true
        };
    }
}
