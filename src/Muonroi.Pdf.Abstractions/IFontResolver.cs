namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Resolves <c>@font-face</c> declarations to font bytes.
/// </summary>
/// <remarks>
/// Bytes-only contract — the engine never receives a file path from the resolver.
/// This prevents path-traversal escapes through <c>@font-face src: url(file://...)</c>.
/// Implementations are responsible for any caching, tenant scoping, and access control.
/// </remarks>
public interface IFontResolver
{
    /// <summary>
    /// Attempts to resolve a font family + style to font file bytes.
    /// </summary>
    /// <param name="request">Font request (family, weight, style).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Font bytes, or null if no match. Bytes must be a valid TTF/OTF blob.</returns>
    ValueTask<ReadOnlyMemory<byte>?> ResolveAsync(FontRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Font selection request.
/// </summary>
public sealed record FontRequest(
    string Family,
    FontWeight Weight = FontWeight.Normal,
    FontStyle Style = FontStyle.Normal);

/// <summary>Standard CSS font-weight values supported by the engine.</summary>
public enum FontWeight
{
    /// <summary>weight: 100</summary>
    Thin = 100,
    /// <summary>weight: 300</summary>
    Light = 300,
    /// <summary>weight: 400</summary>
    Normal = 400,
    /// <summary>weight: 500</summary>
    Medium = 500,
    /// <summary>weight: 600</summary>
    SemiBold = 600,
    /// <summary>weight: 700</summary>
    Bold = 700,
    /// <summary>weight: 900</summary>
    Black = 900
}

/// <summary>CSS font-style values.</summary>
public enum FontStyle
{
    /// <summary>style: normal</summary>
    Normal = 0,
    /// <summary>style: italic</summary>
    Italic = 1,
    /// <summary>style: oblique</summary>
    Oblique = 2
}
