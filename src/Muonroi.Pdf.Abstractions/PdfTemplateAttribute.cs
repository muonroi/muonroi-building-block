namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Marks a model class for compile-time IMPdfRenderer&lt;TModel&gt; generation.
/// The source generator discovers classes decorated with this attribute and emits
/// a sealed renderer implementation with the template HTML inlined as C# string interpolation.
/// </summary>
/// <remarks>
/// <para>
/// <c>templateId</c> is the stable identifier returned by the generated renderer's
/// <c>IMPdfRenderer.TemplateId</c> property, and the key used when resolving the renderer via DI.
/// </para>
/// <para>
/// <c>templateResourceName</c> is the path (relative to the consuming project root)
/// of the HTML template file declared as an <c>AdditionalFiles</c> item in the consumer's csproj.
/// When null, the generator emits a stub renderer with an empty HTML string — useful for
/// defining the DI registration skeleton before the template is ready.
/// </para>
/// </remarks>
/// <remarks>
/// Initialises a new <see cref="PdfTemplateAttribute"/>.
/// </remarks>
/// <param name="templateId">
/// Stable identifier for this template. Must be non-empty. Matches <c>IMPdfRenderer.TemplateId</c>.
/// </param>
/// <param name="templateResourceName">
/// Path of the HTML template relative to the consuming project root, declared as
/// <c>AdditionalFiles</c> in the csproj. Null emits a stub renderer with empty HTML.
/// </param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class PdfTemplateAttribute(string templateId, string? templateResourceName = null) : Attribute
{

    /// <summary>Stable template identifier. Matches <c>IMPdfRenderer.TemplateId</c>.</summary>
    public string TemplateId { get; } = templateId;

    /// <summary>
    /// AdditionalFiles path (relative to project root) of the HTML template file.
    /// Null means the generator emits a stub renderer with an empty HTML string.
    /// </summary>
    public string? TemplateResourceName { get; } = templateResourceName;
}
