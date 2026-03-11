using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions.Adapters;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Renders a Liquid template as an <see cref="IRule{TContext}"/> action.
/// Always evaluates to <see cref="RuleResult.Passed()"/> on successful render;
/// <see cref="RuleResult.Failure(string[])"/> if rendering throws.
/// Rendered output is written to <see cref="FactBag"/> under <c>LiquidOutputKey</c>.
/// </summary>
/// <remarks>
/// Phase A: simple {{ key }} token replacement (no external Liquid engine dependency).
/// Phase B upgrade path: replace <c>RenderAsync</c> with Fluid engine for full Liquid syntax.
/// </remarks>
/// <typeparam name="TContext">The rule execution context type.</typeparam>
public sealed class LiquidRuleAdapter<TContext> : IRule<TContext>
{
    private readonly string _code;
    private readonly string _template;
    private readonly string _outputFormat;  // json|text|object
    private readonly string _outputKey;     // FactBag key for rendered output
    private readonly IContextProjector<TContext> _projector;
    private readonly IMJsonSerializeService _json;
    private readonly IMLog<LiquidRuleAdapter<TContext>> _log;

    public string Code => _code;
    public int Order { get; init; }
    public string[] DependsOn { get; init; } = [];
    public HookPoint HookPoint => HookPoint.BeforeRule;
    public RuleType Type => RuleType.Business; // actions modify state
    public string Name => $"Liquid:{_code}";
    public IEnumerable<Type> Dependencies => [];

    public LiquidRuleAdapter(
        string code,
        string template,
        string outputFormat,
        string outputKey,
        IContextProjector<TContext> projector,
        IMJsonSerializeService json,
        IMLog<LiquidRuleAdapter<TContext>> log)
    {
        _code         = code;
        _template     = template;
        _outputFormat = outputFormat;
        _outputKey    = string.IsNullOrWhiteSpace(outputKey) ? "liquidOutput" : outputKey;
        _projector    = projector;
        _json         = json;
        _log          = log;
    }

    public async Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        Dictionary<string, object?> variables = BuildVariables(ctx, facts);

        string rendered;
        try
        {
            rendered = await RenderAsync(_template, variables, ct);
        }
        catch (Exception ex)
        {
            _log.Warn("Liquid template '{Code}' threw: {Message}", _code, ex.Message);
            return RuleResult.Failure($"Liquid render error in '{_code}': {ex.Message}");
        }

        // Parse output according to requested format
        object? output = _outputFormat switch
        {
            "json" or "object" => _json.Deserialize<object>(rendered),
            _                  => rendered
        };

        facts.Set(_outputKey, output);

        return RuleResult.Passed();
    }

    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private static Task<string> RenderAsync(
        string template,
        IDictionary<string, object?> variables,
        CancellationToken ct)
    {
        _ = ct;
        // Phase A: simple {{ key }} token replacement
        // Phase B: upgrade to Fluid (Fluid.Core NuGet) for {% if %}, {% for %}, filters
        string result = template;
        foreach (KeyValuePair<string, object?> kv in variables)
        {
            string placeholder1 = "{{ " + kv.Key + " }}";
            string placeholder2 = "{{" + kv.Key + "}}";
            string replacement  = kv.Value?.ToString() ?? string.Empty;
            result = result.Replace(placeholder1, replacement, StringComparison.Ordinal);
            result = result.Replace(placeholder2, replacement, StringComparison.Ordinal);
        }

        return Task.FromResult(result);
    }

    private Dictionary<string, object?> BuildVariables(TContext ctx, FactBag facts)
    {
        Dictionary<string, object?> dict = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, object?> kv in _projector.Project(ctx))
        {
            dict[kv.Key] = kv.Value;
        }

        foreach (string key in facts.Keys)
        {
            dict[key] = facts.Get<object>(key);
        }

        return dict;
    }
}
