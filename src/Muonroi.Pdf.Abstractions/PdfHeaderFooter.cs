namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Header or footer content rendered into the page margin box.
/// </summary>
/// <remarks>
/// Supports CSS spec page counters via <c>counter(page)</c> and <c>counter(pages)</c> only.
/// wkhtmltopdf-style tokens such as <c>[page]</c>, <c>[topage]</c>, <c>[date]</c> are not supported —
/// migrate templates to CSS counters or the <see cref="LeftHtml"/> / <see cref="CenterHtml"/> / <see cref="RightHtml"/> fragments.
/// </remarks>
public sealed record PdfHeaderFooter(
    string? LeftHtml = null,
    string? CenterHtml = null,
    string? RightHtml = null,
    double HeightMm = 12,
    bool ShowLine = false);
