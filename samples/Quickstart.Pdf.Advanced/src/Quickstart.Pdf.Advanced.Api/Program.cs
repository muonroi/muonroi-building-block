using Muonroi.Pdf.Enterprise;
using Muonroi.Pdf.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// OSS PDF engine  (Muonroi.Pdf)
//
// AddPdf(configuration) registers the full HTML/CSS -> PDF pipeline and exposes
// IMPdfService. PdfConfigs is bound from the "PdfConfigs" section and validated
// at startup (all limits must be positive). No external services required.
// -------------------------------------------------------------------------
builder.Services.AddPdf(builder.Configuration);

// -------------------------------------------------------------------------
// Enterprise capability gate  (Muonroi.Pdf.Enterprise)
//
// Muonroi.Pdf.Enterprise ships no DI extension — it is a toolkit of static
// helpers (SsimScorer, PngDecoder, CapabilityKeys) plus the IFeatureGate
// contract and the IMPdfTemplateRegistry client interface. For OSS / dev use,
// register AlwaysAllowFeatureGate so EnsureFeatureOrThrow never blocks.
// -------------------------------------------------------------------------
builder.Services.AddSingleton(AlwaysAllowFeatureGate.Instance);

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Pdf.Advanced API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Pdf.DesignSystem.Default + Muonroi.Pdf.Enterprise: " +
                      "DesignSystemTemplateProvider serves the embedded invoice/receipt/report " +
                      "templates, which IMPdfService (AddPdf) renders to PDF. The Enterprise " +
                      "toolkit is shown via SsimScorer (visual-regression quality gate), the " +
                      "AlwaysAllowFeatureGate / CapabilityKeys capability model, and the " +
                      "IMPdfTemplateRegistry contracts."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
