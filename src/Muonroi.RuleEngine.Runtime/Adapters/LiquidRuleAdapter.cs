using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions.Adapters;
using Scriban;
using Scriban.Runtime;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Renders a Scriban/Liquid template as an <see cref="IRule{TContext}"/> action.
/// Always evaluates to <see cref="RuleResult.Passed()"/> on successful render;
/// <see cref="RuleResult.Failure(string[])"/> if rendering throws.
/// Rendered output is written to <see cref="FactBag"/> under <c>LiquidOutputKey</c>.
/// </summary>
/// <remarks>
/// Upgraded from Phase A (simple {{ key }} replacement) to Scriban engine.
/// Scriban is Liquid-compatible — existing {{ key }} templates continue working.
/// Supports full control flow (if/else, for), filters, nested access, and async rendering.
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
    private readonly IEnumerable<IScribanFunctionProvider>? _functionProviders;
    private Template? _parsedTemplate;

    /// <inheritdoc />
    public string Code => _code;

    /// <inheritdoc />
    public int Order { get; init; }

    /// <inheritdoc />
    public string[] DependsOn { get; init; } = [];

    /// <inheritdoc />
    public HookPoint HookPoint => HookPoint.BeforeRule;

    /// <inheritdoc />
    public RuleType Type => RuleType.Business; // actions modify state

    /// <inheritdoc />
    public string Name => $"Liquid:{_code}";

    /// <inheritdoc />
    public IEnumerable<Type> Dependencies => [];

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidRuleAdapter{TContext}"/> class.
    /// </summary>
    /// <param name="code">Rule code for this node.</param>
    /// <param name="template">Liquid/Scriban template.</param>
    /// <param name="outputFormat">Output format (json|text|object).</param>
    /// <param name="outputKey">FactBag key to store rendered output.</param>
    /// <param name="projector">Context projector for variables.</param>
    /// <param name="json">JSON serializer.</param>
    /// <param name="log">Logger instance.</param>
    /// <param name="functionProviders">Optional custom Scriban function providers.</param>
    public LiquidRuleAdapter(
        string code,
        string template,
        string outputFormat,
        string outputKey,
        IContextProjector<TContext> projector,
        IMJsonSerializeService json,
        IMLog<LiquidRuleAdapter<TContext>> log,
        IEnumerable<IScribanFunctionProvider>? functionProviders = null)
    {
        _code         = code;
        _template     = template;
        _outputFormat = outputFormat;
        _outputKey    = string.IsNullOrWhiteSpace(outputKey) ? "liquidOutput" : outputKey;
        _projector    = projector;
        _json         = json;
        _log          = log;
        _functionProviders = functionProviders;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private async Task<string> RenderAsync(
        string template,
        IDictionary<string, object?> variables,
        CancellationToken ct)
    {
        _parsedTemplate ??= Template.Parse(template);

        if (_parsedTemplate.HasErrors)
        {
            string errors = string.Join("; ", _parsedTemplate.Messages);
            throw new InvalidOperationException($"Scriban parse error: {errors}");
        }

        ScribanFactBagScriptObject scriptObject = new(variables);

        // Register custom function providers
        if (_functionProviders is not null)
        {
            foreach (IScribanFunctionProvider provider in _functionProviders)
            {
                provider.Register(scriptObject);
            }
        }

        TemplateContext context = new()
        {
            StrictVariables = false,
            MemberRenamer = member => member.Name,  // Preserve original casing
        };
        context.PushGlobal(scriptObject);

        string result = await _parsedTemplate.RenderAsync(context);
        return result;
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
