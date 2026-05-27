using Muonroi.Pdf.Abstractions;

namespace Muonroi.Pdf.Benchmarks;

/// <summary>
/// SC2 benchmark model. The <see cref="PdfTemplateAttribute"/> drives
/// <c>Muonroi.Pdf.SourceGenerators</c> to emit a strongly-typed
/// <c>IMPdfRenderer&lt;InvoiceBenchModel&gt;</c> in the
/// <c>Muonroi.Pdf.Benchmarks.Generated</c> namespace, with <c>invoice-bench.html</c> inlined as a
/// compile-time interpolated string (no runtime template load / token substitution).
/// </summary>
[PdfTemplate("invoice-bench", "invoice-bench.html")]
public sealed partial class InvoiceBenchModel
{
    public string CompanyName { get; set; } = "Muonroi Technology Solutions Co., Ltd.";
    public string InvoiceNumber { get; set; } = "INV-2024-000001";
    public string InvoiceDate { get; set; } = "2024-01-15";
    public string CustomerName { get; set; } = "Acme Corporation International";
    public string CustomerAddress { get; set; } = "456 Business Ave Suite 800, New York NY 10001 USA";
    public string LineItem1 { get; set; } = "PDF Engine Core Library Annual License";
    public string Amount1 { get; set; } = "$3,500.00";
    public string LineItem2 { get; set; } = "Extended Gold Support Package (12-month)";
    public string Amount2 { get; set; } = "$2,700.00";
    public string LineItem3 { get; set; } = "Vietnamese Font Pack (Noto Sans, full diacritics)";
    public string Amount3 { get; set; } = "$230.00";
    public string Total { get; set; } = "$6,430.00";
}
