using Microsoft.OpenApi.Models;
using Quickstart.Pdf.SourceGenerators.Api.Models;
using Quickstart.Pdf.SourceGenerators.Api.Models.Generated;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// --- Feature-specific registrations ---
builder.Services.AddPdf(builder.Configuration);

// Register the source-generated renderer
builder.Services.AddPdfRendererReportModel();

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Quickstart.Pdf.SourceGenerators API",
        Version = "v1",
        Description = "Demonstrates compile-time PDF renderer generation."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapPost("/pdf/report", async (IMPdfRenderer<ReportModel> renderer, [FromBody] ReportModel model, CancellationToken ct) =>
{
    var memoryStream = new MemoryStream();
    
    // The renderer uses the inlined HTML template and binds the model properties at compile-time.
    var metadata = await renderer.RenderAsync(model, memoryStream, null, ct);
    
    return Results.File(memoryStream.ToArray(), "application/pdf", "report.pdf");
});

app.Run();
