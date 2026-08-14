namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Renders a template as an <see cref="IRule{TContext}"/> action.
/// Always evaluates to <see cref="RuleResult.Passed()"/> on successful render;
/// <see cref="RuleResult.Failure(string[])"/> if rendering throws.
/// Rendered output is written to <see cref="FactBag"/> under <c>LiquidOutputKey</c>.
/// </summary>
public sealed class LiquidRuleAdapter<TContext> : IRule<TContext>
{
    private readonly string _code;
    private readonly string _template;
    private readonly string _outputFormat;
    private readonly string _outputKey;
    private readonly IContextProjector<TContext> _projector;
    private readonly IMJsonSerializeService _json;
    private readonly IMLog<LiquidRuleAdapter<TContext>> _log;
    private readonly ITemplateEngine? _templateEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiquidRuleAdapter{TContext}"/> class.
    /// </summary>
    /// <param name="code">The node code.</param>
    /// <param name="template">The template.</param>
    /// <param name="outputFormat">The output format.</param>
    /// <param name="outputKey">The output key.</param>
    /// <param name="projector">The context projector.</param>
    /// <param name="json">The json service.</param>
    /// <param name="log">The logger.</param>
    /// <param name="templateEngine">The template engine.</param>
    public LiquidRuleAdapter(
        string code,
        string template,
        string outputFormat,
        string outputKey,
        IContextProjector<TContext> projector,
        IMJsonSerializeService json,
        IMLog<LiquidRuleAdapter<TContext>> log,
        ITemplateEngine? templateEngine = null)
    {
        _code = code;
        _template = template;
        _outputFormat = outputFormat;
        _outputKey = string.IsNullOrWhiteSpace(outputKey) ? "liquidOutput" : outputKey;
        _projector = projector;
        _json = json;
        _log = log;
        _templateEngine = templateEngine;
    }

    /// <inheritdoc />
    public string Code => _code;

    /// <inheritdoc />
    public int Order { get; init; }

    /// <inheritdoc />
    public string[] DependsOn { get; init; } = [];

    /// <inheritdoc />
    public HookPoint HookPoint => HookPoint.BeforeRule;

    /// <inheritdoc />
    public RuleType Type => RuleType.Business;

    /// <inheritdoc />
    public string Name => $"Liquid:{_code}";

    /// <inheritdoc />
    public IEnumerable<Type> Dependencies => [];

    /// <inheritdoc />
    public async Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        if (_templateEngine == null)
        {
            _log.Warn("No ITemplateEngine registered. Cannot evaluate liquid template '{Code}'.", _code);
            return RuleResult.Failure($"No ITemplateEngine registered for '{_code}'.");
        }

        Dictionary<string, object?> variables = BuildVariables(ctx, facts);

        string rendered;
        try
        {
            rendered = await _templateEngine.RenderAsync(_template, variables, ct);
        }
        catch (Exception ex)
        {
            _log.Warn("Liquid template '{Code}' threw: {Message}", _code, ex.Message);
            return RuleResult.Failure($"Liquid render error in '{_code}': {ex.Message}");
        }

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
