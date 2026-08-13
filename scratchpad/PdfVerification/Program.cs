


namespace PdfVerification;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("Starting PDF Engine Verification...");

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        // Enable modern layout and soften unknown CSS property handling
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PdfConfigs:Policy:AllowModernLayout"] = "true",
            ["PdfConfigs:Policy:SoftDegradeUnknownDisplay"] = "true"
        });

        // Register the PDF service
        builder.Services.AddPdf(builder.Configuration);

        using IHost host = builder.Build();
        IMPdfService pdfService = host.Services.GetRequiredService<IMPdfService>();

        // Directory containing diagnostic HTML files
        string diagFolder = Path.Combine(Directory.GetCurrentDirectory(), "pdf-diag");
        if (!Directory.Exists(diagFolder))
        {
            Console.WriteLine($"Diagnostic folder not found: {diagFolder}");
            return;
        }

        foreach (var htmlPath in Directory.GetFiles(diagFolder, "*.html"))
        {
            string htmlContent = await File.ReadAllTextAsync(htmlPath);
            string baseName = Path.GetFileNameWithoutExtension(htmlPath);
            string pdfPath = Path.Combine(Directory.GetCurrentDirectory(), $"{baseName}.pdf");
            if (File.Exists(pdfPath))
            {
                File.Delete(pdfPath);
            }

            Console.WriteLine($"Rendering {Path.GetFileName(htmlPath)} → {Path.GetFileName(pdfPath)}");
            using FileStream outputStream = File.Create(pdfPath);
            try
            {
                PdfRenderResult result = await pdfService.RenderAsync(htmlContent, outputStream, new PdfRenderOptions
                {
                    PageSize = PdfPageSize.A4,
                    Orientation = PdfOrientation.Portrait,
                    Margins = PdfMargins.Uniform(15),
                    TemplateId = "pdf-verification-template"
                });
                Console.WriteLine($"Generated {pdfPath}: {result.PageCount} pages, {result.ByteCount} bytes");
                if (result.Diagnostics.Count > 0)
                {
                    Console.WriteLine("Diagnostics:");
                    foreach (PolicyViolation diag in result.Diagnostics)
                    {
                        Console.WriteLine($"- [{diag.Severity}] Rule: {diag.RuleId}, Prop: {diag.PropertyName}, Value: {diag.RejectedValue}, Selector: {diag.CssSelector}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error rendering {htmlPath}: {ex.Message}");
            }
        }
    }
}
