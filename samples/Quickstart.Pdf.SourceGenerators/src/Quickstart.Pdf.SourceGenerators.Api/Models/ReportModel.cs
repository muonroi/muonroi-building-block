using Muonroi.Pdf.Abstractions;

namespace Quickstart.Pdf.SourceGenerators.Api.Models;

[PdfTemplate("ReportModel-v1", "Templates/ReportTemplate.html")]
public partial class ReportModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
}
