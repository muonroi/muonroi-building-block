namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Standard PDF page sizes supported by the rendering engine.
/// </summary>
public enum PdfPageSize
{
    /// <summary>A4 paper (210 x 297 mm).</summary>
    A4 = 0,

    /// <summary>A5 paper (148 x 210 mm).</summary>
    A5 = 1,

    /// <summary>A3 paper (297 x 420 mm).</summary>
    A3 = 2,

    /// <summary>US Letter (8.5 x 11 in).</summary>
    Letter = 3,

    /// <summary>US Legal (8.5 x 14 in).</summary>
    Legal = 4
}
