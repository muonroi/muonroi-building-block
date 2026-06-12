namespace Muonroi.Pdf.Tests;

/// <summary>
/// Test collection for PDF render tests. OwnedPdfWriter has no process-global mutable state,
/// so parallelization is safe.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = false)]
public sealed class PdfRenderCollection
{
    public const string Name = "PdfRender";
}
