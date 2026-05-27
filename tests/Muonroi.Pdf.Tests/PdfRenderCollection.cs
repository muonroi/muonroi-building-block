namespace Muonroi.Pdf.Tests;

/// <summary>
/// Serializes every test that drives <c>PdfSharpCoreWriter</c>. PdfSharpCore keeps process-global
/// mutable font state (<c>GlobalFontSettings.FontResolver</c> set once per process, plus a static
/// <c>FontFactory</c> source cache keyed by the font's internal name). Two render tests in
/// different xunit collections run in parallel and race on that shared state, producing
/// intermittent "same key already added" / NullReferenceException failures. Assigning all
/// renderers to one non-parallel collection removes the race deterministically.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PdfRenderCollection
{
    public const string Name = "PdfRender";
}
