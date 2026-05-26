namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Page orientation for the rendered PDF.
/// </summary>
public enum PdfOrientation
{
    /// <summary>Taller than wide.</summary>
    Portrait = 0,

    /// <summary>Wider than tall.</summary>
    Landscape = 1
}
