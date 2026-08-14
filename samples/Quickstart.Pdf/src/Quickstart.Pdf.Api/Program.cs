WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// --- Feature-specific registrations ---
builder.Services.AddPdf(builder.Configuration);

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Quickstart.Pdf API",
        Version = "v1",
        Description = "Demonstrates basic PDF rendering capabilities."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapGet("/pdf/invoice", async (IMPdfService pdfService, CancellationToken ct) =>
{
    var html = @"
        <html>
            <head>
                <style>
                    body { font-family: Helvetica, sans-serif; margin: 0; padding: 20px; }
                    h1 { color: #2c3e50; }
                    .invoice-box { padding: 30px; border: 1px solid #eee; box-shadow: 0 0 10px rgba(0, 0, 0, 0.15); }
                    .amount { font-size: 24px; font-weight: bold; color: #e74c3c; }
                </style>
            </head>
            <body>
                <div class='invoice-box'>
                    <h1>Invoice #INV-2026-001</h1>
                    <p>Date: August 14, 2026</p>
                    <hr/>
                    <p>Services rendered for Muonroi Ecosystem.</p>
                    <p class='amount'>Total: $1,250.00</p>
                </div>
            </body>
        </html>";

    var options = new PdfRenderOptions 
    { 
        TemplateId = "Invoice-INV-2026-001",
        Margins = PdfMargins.Default10mm
    };
    
    var (bytes, metadata) = await pdfService.RenderToBytesAsync(html, options, ct);
    
    // Add custom header to see the metadata (template hash, byte count)
    var ctx = app.Services.GetRequiredService<IHttpContextAccessor>()?.HttpContext;
    if (ctx != null)
    {
        ctx.Response.Headers.Append("X-Pdf-Page-Count", metadata.PageCount.ToString());
        ctx.Response.Headers.Append("X-Pdf-Template-Hash", metadata.TemplateHash);
    }
    
    return Results.File(bytes, "application/pdf", "invoice.pdf");
});

app.Run();
