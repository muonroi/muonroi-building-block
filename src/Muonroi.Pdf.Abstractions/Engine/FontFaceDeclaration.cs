namespace Muonroi.Pdf.Abstractions.Engine;

/// <summary>
/// Represents a resolved CSS <c>@font-face</c> declaration extracted from the styled document,
/// used by the font resolver to locate and load the matching font file.
/// </summary>
/// <param name="Family">CSS font-family name as declared in the <c>@font-face</c> rule (e.g. <c>"Arial"</c>).</param>
/// <param name="Weight">Numeric font weight (e.g. <see cref="FontWeight.Normal"/> or <see cref="FontWeight.Bold"/>).</param>
/// <param name="Style">Font style variant (e.g. <see cref="FontStyle.Normal"/> or <see cref="FontStyle.Italic"/>).</param>
public sealed record FontFaceDeclaration(string Family, FontWeight Weight, FontStyle Style);
