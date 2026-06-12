namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Strongly-typed per-template renderer. Implementations are emitted by the source generator
/// (v0.2+) from a compile-time template, or hand-written for hot paths.
/// </summary>
/// <typeparam name="TModel">Template model type.</typeparam>
/// <remarks>
/// In v0.1, factory returns a runtime-backed wrapper that performs Scriban (or equivalent)
/// substitution + delegates to <see cref="IMPdfService"/>. The interface ships in v0.1 so the
/// v0.2 source generator can populate it without breaking changes.
/// </remarks>
public interface IMPdfRenderer<in TModel>
{
    /// <summary>Template identifier — matches the key used when registering this renderer.</summary>
    string TemplateId { get; }

    /// <summary>Renders the model to PDF, writing to the destination stream.</summary>
    Task<PdfRenderResult> RenderAsync(
        TModel model,
        Stream destination,
        PdfRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory for resolving renderers by template id. Allows hybrid compile-time and runtime
/// renderers to coexist behind a single contract.
/// </summary>
public interface IMPdfRendererFactory
{
    /// <summary>
    /// Resolves a renderer for the given template id. Throws when the template is unknown.
    /// </summary>
    IMPdfRenderer<TModel> Get<TModel>(string templateId);

    /// <summary>
    /// Attempts to resolve a renderer. Returns false when the template id is not registered.
    /// </summary>
    bool TryGet<TModel>(string templateId, out IMPdfRenderer<TModel>? renderer);
}
