namespace Muonroi.Templating.Scriban;

/// <summary>
/// A template engine implementation using Scriban.
/// </summary>
public sealed class ScribanTemplateEngine : ITemplateEngine
{
    private readonly IEnumerable<IScribanFunctionProvider>? _functionProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScribanTemplateEngine"/> class.
    /// </summary>
    /// <param name="functionProviders">Optional custom function providers.</param>
    public ScribanTemplateEngine(IEnumerable<IScribanFunctionProvider>? functionProviders = null)
    {
        _functionProviders = functionProviders;
    }

    /// <inheritdoc />
    public async Task<string> RenderAsync(string template, IDictionary<string, object?> variables, CancellationToken cancellationToken = default)
    {
        Template parsedTemplate = Template.Parse(template);

        if (parsedTemplate.HasErrors)
        {
            string errors = string.Join("; ", parsedTemplate.Messages);
            return MGuard.Fail<string>($"Scriban parse error: {errors}");
        }

        ScribanFactBagScriptObject scriptObject = new(variables);

        if (_functionProviders != null)
        {
            foreach (IScribanFunctionProvider provider in _functionProviders)
            {
                provider.Register(scriptObject);
            }
        }

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        TemplateContext context = new()
        {
            StrictVariables = false,
            MemberRenamer = member => member.Name,
            LoopLimit = 10_000,
            CancellationToken = cts.Token,
        };
        context.PushGlobal(scriptObject);

        return await parsedTemplate.RenderAsync(context);
    }

    /// <inheritdoc />
    public string Render(string template, IDictionary<string, object?> variables)
    {
        return RenderAsync(template, variables, CancellationToken.None).GetAwaiter().GetResult();
    }
}
