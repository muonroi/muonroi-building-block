namespace Muonroi.Pdf.Internal.Layout.Geometry;

internal static class PdfPageSizeDimensions
{
    /// <summary>Returns (widthPt, heightPt) for portrait orientation.</summary>
    public static (float Width, float Height) Get(Abstractions.PdfPageSize size) => size switch
    {
        Abstractions.PdfPageSize.A4 => (595.28f, 841.89f),
        Abstractions.PdfPageSize.A5 => (419.53f, 595.28f),
        Abstractions.PdfPageSize.A3 => (841.89f, 1190.55f),
        Abstractions.PdfPageSize.Letter => (612f, 792f),
        Abstractions.PdfPageSize.Legal => (612f, 1008f),
        _ => (595.28f, 841.89f)
    };
}
