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
            PdfSharpCore.Fonts.IFontResolver? previous = GlobalFontSettings.FontResolver;
            try
            {
                GlobalFontSettings.FontResolver = new PdfSharpFontResolverAdapter(pageList.EmbeddedFonts);
                RenderDocument(pageList, options, ms, ct);
            }
            finally
            {
                GlobalFontSettings.FontResolver = previous;
            }
        }

        byte[] pdfBytes = ms.ToArray();

        // Normalize the PDF version header to 1.7 (PdfSharpCore defaults to 1.3/1.4).
        if (pdfBytes.Length >= 8 &&
            pdfBytes[0] == (byte)'%' && pdfBytes[1] == (byte)'P' && pdfBytes[2] == (byte)'D' &&
            pdfBytes[3] == (byte)'F' && pdfBytes[4] == (byte)'-' && pdfBytes[5] == (byte)'1' &&
            pdfBytes[6] == (byte)'.')
        {
            pdfBytes[7] = (byte)'7';
        }

        await destination.WriteAsync(pdfBytes, 0, pdfBytes.Length, ct).ConfigureAwait(false);
        return pdfBytes.Length;
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
        catch (Exception)
        {
            // Property read-only on some PdfSharpCore builds; the 05-03 determinism test
            // will catch any residual non-determinism from timestamps.
        }

        (double w, double h) = GetPageDimensions(options);

        foreach (PositionedPage page in pageList.Pages)
        {
            RenderPage(doc, page, pageList.Images, w, h, ct);
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
                    var font = new XFont(inline.FontFamily, inline.FontSize, style);
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
