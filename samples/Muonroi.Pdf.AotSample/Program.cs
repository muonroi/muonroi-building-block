// AOT-02 smoke test: renders a minimal HTML document to PDF using the Muonroi.Pdf engine.
// This sample intentionally omits Muonroi.Observability (OtelSetup has AOT-incompatible
// AppDomain.CurrentDomain.GetAssemblies() + Activator.CreateInstance) and Muonroi.Tenancy
// (ITenantContextPolicy has AppDomain reflection). The render path under NativeAOT is exercised
// by the Docker publish step in Plan 05. This host build validates DI wiring and compile-time correctness.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Extensions;

try
{
    var config = new ConfigurationBuilder().Build();
    var services = new ServiceCollection();
    services.AddPdf(config);
    var provider = services.BuildServiceProvider();
    var pdfService = provider.GetRequiredService<IMPdfService>();

    const string html = "<!DOCTYPE html><html><body><h1>AOT Sample</h1><p>Hello from NativeAOT.</p></body></html>";
    var outputPath = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "aot-sample-output.pdf");

    await using var output = File.OpenWrite(outputPath);
    var result = await pdfService.RenderAsync(html, output, new PdfRenderOptions());
    Console.WriteLine($"OK: {result.PageCount}p {result.ByteCount}b -> {outputPath}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"AOT render FAILED: {ex}");
    return 1;
}
