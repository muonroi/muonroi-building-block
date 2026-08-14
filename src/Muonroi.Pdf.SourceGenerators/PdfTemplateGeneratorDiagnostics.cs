namespace Muonroi.Pdf.SourceGenerators;

/// <summary>
/// Diagnostic descriptors for PdfTemplateGenerator.
/// </summary>
internal static class PdfTemplateGeneratorDiagnostics
{
    /// <summary>
    /// PDFSG0001: Warning — class annotated with [PdfTemplate] is not partial.
    /// Generator still emits but warns; marking the class partial is recommended
    /// so that consumers can extend the generated renderer.
    /// </summary>
    public static readonly DiagnosticDescriptor NotPartial = new DiagnosticDescriptor(
        id: "PDFSG0001",
        title: "PdfTemplate class is not partial",
        messageFormat: "Class '{0}' is decorated with [PdfTemplate] but is not declared partial. Consider making it partial so the generated renderer can be extended.",
        category: "Muonroi.Pdf.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Classes decorated with [PdfTemplate] should be declared partial to enable future extensibility of the generated renderer.");

    /// <summary>
    /// PDFSG0002: Error — [PdfTemplate] templateId is null or empty.
    /// The generator cannot emit a valid renderer without a stable template identifier.
    /// </summary>
    public static readonly DiagnosticDescriptor EmptyTemplateId = new DiagnosticDescriptor(
        id: "PDFSG0002",
        title: "PdfTemplate templateId is null or empty",
        messageFormat: "Class '{0}' has a [PdfTemplate] attribute with a null or empty templateId. A non-empty templateId is required.",
        category: "Muonroi.Pdf.SourceGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [PdfTemplate] attribute requires a non-empty templateId string that uniquely identifies the template.");
}
