namespace Muonroi.Templating.Abstractions;

/// <summary>
/// Defines an engine capable of rendering templates (e.g. Scriban, Liquid).
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Renders the given template asynchronously using the provided variables.
    /// </summary>
    /// <param name="template">The template string.</param>
    /// <param name="variables">The variables available in the template.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rendered string.</returns>
    Task<string> RenderAsync(string template, IDictionary<string, object?> variables, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders the given template synchronously using the provided variables.
    /// </summary>
    /// <param name="template">The template string.</param>
    /// <param name="variables">The variables available in the template.</param>
    /// <returns>The rendered string.</returns>
    string Render(string template, IDictionary<string, object?> variables);
}
