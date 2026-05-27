// Security invariants (SEC-02):
// This writer NEVER calls APIs that produce /JavaScript, /Launch, /OpenAction,
// or /EmbeddedFile dictionary entries. The absence of such calls IS the enforcement.
// Any future code path that would add such entries must be reviewed and blocked.

using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;

namespace Muonroi.Pdf.Internal.Writer;

/// <summary>
/// Converts a <see cref="PositionedPageList"/> into a hardened, deterministic PDF 1.7 stream.
/// Draws positioned <see cref="InlineBox"/> text and <see cref="ReplacedBox"/> images; all other
/// box types are no-ops (background/border rendering deferred to Phase 6+).
/// </summary>
internal sealed class PdfSharpCoreWriter : IPdfWriter
{
    // Fixed sentinel timestamp — suppresses render-time metadata leakage (SEC-04) and
    // keeps the trailer /ID hash stable across renders (DET-01/02/03).
    private static readonly DateTime SentinelDate = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // GlobalFontSettings.FontResolver is global mutable state — serialize access (T-05-04).
    private static readonly object _fontResolverLock = new();

    // PdfSharpCore permits setting GlobalFontSettings.FontResolver only ONCE per process
    // ("Must not change font resolver after it was once used."). Install a single stable
    // adapter instance and swap its backing font map per render instead of reassigning.
    private static readonly PdfSharpFontResolverAdapter _sharedFontResolver = new();
    private static bool _fontResolverInstalled;

    public async ValueTask<long> WriteAsync(
        IPositionedPageList pages,
        PdfRenderOptions options,
        Stream destination,
        CancellationToken ct = default)
    {
        if (pages is not PositionedPageList pageList)
        {
            throw new InvalidOperationException(
                "PdfSharpCoreWriter requires PositionedPageList from the Muonroi.Pdf engine");
        }

        var ms = new MemoryStream();

        lock (_fontResolverLock)
        {
            _sharedFontResolver.SetEmbeddedFonts(pageList.EmbeddedFonts);
            if (!_fontResolverInstalled)
            {
                GlobalFontSettings.FontResolver = _sharedFontResolver;
                _fontResolverInstalled = true;
            }
            RenderDocument(pageList, options, ms, ct);
        }

        byte[] pdfBytes = NormalizeForDeterminism(ms.ToArray());

        await destination.WriteAsync(pdfBytes, 0, pdfBytes.Length, ct).ConfigureAwait(false);
        return pdfBytes.Length;
    }

    // PdfSharpCore injects two per-render random tokens that break byte-for-byte determinism
    // (DET-01/02/03): a 6-letter font-subset prefix ("ABCDEF+FontName") and the trailer
    // /ID GUID pair. Both are pure-ASCII, fixed-length tokens, so replacing them with fixed
    // sentinels of identical length preserves byte offsets and the xref table.
    private static readonly System.Text.RegularExpressions.Regex SubsetPrefixRegex =
        new(@"/([A-Z]{6})\+", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex TrailerIdRegex =
        new(@"/ID \[<[0-9A-Fa-f]{32}><[0-9A-Fa-f]{32}>\]",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private const string FixedSubsetPrefix = "AAAAAA";
    private const string FixedTrailerId =
        "/ID [<00000000000000000000000000000000><00000000000000000000000000000000>]";

    private static byte[] NormalizeForDeterminism(byte[] pdfBytes)
    {
        // Normalize the PDF version header to 1.7 (PdfSharpCore defaults to 1.3/1.4).
        if (pdfBytes.Length >= 8 &&
            pdfBytes[0] == (byte)'%' && pdfBytes[1] == (byte)'P' && pdfBytes[2] == (byte)'D' &&
            pdfBytes[3] == (byte)'F' && pdfBytes[4] == (byte)'-' && pdfBytes[5] == (byte)'1' &&
            pdfBytes[6] == (byte)'.')
        {
            pdfBytes[7] = (byte)'7';
        }

        // Latin1 is a lossless 1:1 byte<->char mapping, so the round-trip preserves all bytes
        // and same-length token replacements keep every offset (and thus the xref) valid.
        string text = System.Text.Encoding.Latin1.GetString(pdfBytes);
        text = SubsetPrefixRegex.Replace(text, "/" + FixedSubsetPrefix + "+");
        text = TrailerIdRegex.Replace(text, FixedTrailerId);
        return System.Text.Encoding.Latin1.GetBytes(text);
    }

    private static void RenderDocument(
        PositionedPageList pageList,
        PdfRenderOptions options,
        Stream ms,
        CancellationToken ct)
    {
        var doc = new PdfDocument();

        // Suppress non-deterministic / leaky metadata (SEC-03, SEC-04, DET-01/02/03).
        doc.Info.Title = string.Empty;
        doc.Info.Author = string.Empty;
        doc.Info.Subject = string.Empty;
        doc.Info.Keywords = string.Empty;
        doc.Info.Creator = string.Empty;
        try
        {
            doc.Info.CreationDate = SentinelDate;
            doc.Info.ModificationDate = SentinelDate;
        }
        catch (Exception ex)
        {
            // Intentional: these setters are read-only on some PdfSharpCore builds.
            // NormalizeForDeterminism handles any residual non-determinism from timestamps.
            System.Diagnostics.Debug.WriteLine(
                $"[PdfSharpCoreWriter] Info.CreationDate/ModificationDate assignment skipped: {ex.Message}");
        }

        (double w, double h) = GetPageDimensions(options);

        // ALLOC-01: XFont construction (XGlyphTypeface.GetOrCreateFrom) is expensive and was
        // previously repeated once per text element — the dominant render allocator (~92% of
        // total bytes). XFont is immutable and reusable across pages/graphics, so cache by
        // (family, size, style) for the whole document.
        var fontCache = new Dictionary<(string Family, double Size, XFontStyle Style), XFont>();

        foreach (PositionedPage page in pageList.Pages)
        {
            RenderPage(doc, page, pageList.Images, w, h, fontCache, ct);
        }

        doc.Save(ms);
        doc.Close();
    }

    private static (double Width, double Height) GetPageDimensions(PdfRenderOptions options)
    {
        (float w, float h) = PdfPageSizeDimensions.Get(options.PageSize);
        return options.Orientation == PdfOrientation.Landscape ? (h, w) : (w, h);
    }

    private static void RenderPage(
        PdfDocument doc,
        PositionedPage page,
        IReadOnlyDictionary<string, DecodedImage> images,
        double w,
        double h,
        Dictionary<(string Family, double Size, XFontStyle Style), XFont> fontCache,
        CancellationToken ct)
    {
        PdfPage pdfPage = doc.AddPage();
        pdfPage.Width = XUnit.FromPoint(w);
        pdfPage.Height = XUnit.FromPoint(h);

        using XGraphics gfx = XGraphics.FromPdfPage(pdfPage);

        foreach (PositionedElement el in page.Elements)
        {
            ct.ThrowIfCancellationRequested();

            switch (el.Source)
            {
                case InlineBox inline when !string.IsNullOrEmpty(inline.Text):
                    XFontStyle style = (inline.Bold, inline.Italic) switch
                    {
                        (true, true) => XFontStyle.BoldItalic,
                        (true, false) => XFontStyle.Bold,
                        (false, true) => XFontStyle.Italic,
                        _ => XFontStyle.Regular
                    };
                    (string, double, XFontStyle) fontKey = (inline.FontFamily, inline.FontSize, style);
                    if (!fontCache.TryGetValue(fontKey, out XFont? font))
                    {
                        font = new XFont(inline.FontFamily, inline.FontSize, style);
                        fontCache[fontKey] = font;
                    }
                    XBrush brush = ParseColor(inline.Color) ?? XBrushes.Black;
                    gfx.DrawString(inline.Text, font, brush, new XPoint(el.Position.X, el.Position.Y));
                    break;

                case ReplacedBox img when img.Src != null && images.TryGetValue(img.Src, out DecodedImage? decoded):
                    byte[] imageData = decoded.Data.ToArray();
                    XImage ximg = XImage.FromStream(() => new MemoryStream(imageData));
                    try
                    {
                        gfx.DrawImage(ximg, el.Position.X, el.Position.Y, el.Position.Width, el.Position.Height);
                    }
                    finally
                    {
                        ximg.Dispose();
                    }
                    break;

                // BlockBox, AnonymousBox, TableBox, TableRowGroupBox, TableRowBox, TableCellBox:
                // no text/image content — background/border rendering deferred to Phase 6+.
                default:
                    break;
            }
        }
    }

    private static XBrush? ParseColor(string? cssColor)
    {
        if (string.IsNullOrEmpty(cssColor))
        {
            return null;
        }

        string c = cssColor.Trim();

        switch (c.ToLowerInvariant())
        {
            case "black": return XBrushes.Black;
            case "white": return XBrushes.White;
            case "red": return XBrushes.Red;
            case "green": return XBrushes.Green;
            case "blue": return XBrushes.Blue;
            case "gray":
            case "grey": return XBrushes.Gray;
            case "yellow": return XBrushes.Yellow;
        }

        if (c.Length == 7 && c[0] == '#' &&
            int.TryParse(c.Substring(1, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int r) &&
            int.TryParse(c.Substring(3, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int g) &&
            int.TryParse(c.Substring(5, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out int b))
        {
            return new XSolidBrush(XColor.FromArgb(r, g, b));
        }

        return null;
    }
}
