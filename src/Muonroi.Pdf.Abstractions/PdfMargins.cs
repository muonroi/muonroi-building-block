namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Page margins in millimetres. All values clamped to [0, 100] by the engine.
/// </summary>
public sealed record PdfMargins(double TopMm, double RightMm, double BottomMm, double LeftMm)
{
    /// <summary>10 mm uniform margins — the default for reports.</summary>
    public static readonly PdfMargins Default10mm = new(10, 10, 10, 10);

    /// <summary>20 mm uniform margins — typical for letters and contracts.</summary>
    public static readonly PdfMargins Default20mm = new(20, 20, 20, 20);

    /// <summary>Zero margins — caller takes responsibility for safe bleed area.</summary>
    public static readonly PdfMargins Zero = new(0, 0, 0, 0);

    /// <summary>Creates uniform margins from a single millimetre value.</summary>
    public static PdfMargins Uniform(double mm) => new(mm, mm, mm, mm);
}
