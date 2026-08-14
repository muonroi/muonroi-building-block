WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// --- Feature-specific registrations ---
builder.Services.AddPdf(builder.Configuration);

// Explicitly register DefaultStrictPolicy for explicit policy enforcement
builder.Services.AddSingleton<DefaultStrictPolicy>();
builder.Services.AddSingleton<LegacyPrintPolicy>();

// --- Standard wiring ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Quickstart.Pdf.Governance API",
        Version = "v1",
        Description = "Demonstrates PDF engine CSS governance and policy enforcement."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapPost("/pdf/render/modern", async (IMPdfService pdfService, LegacyPrintPolicy policy, CancellationToken ct) =>
{
    var html = @"
        <html>
            <head>
                <style>
                    body { font-family: sans-serif; }
                    .flex-container { 
                        display: flex; 
                        justify-content: space-between; 
                        background: #f0f0f0; 
                        padding: 10px;
                    }
                    .item { padding: 20px; background: #3498db; color: white; }
                </style>
            </head>
            <body>
                <div class='flex-container'>
                    <div class='item'>Left Item</div>
                    <div class='item'>Right Item</div>
                </div>
            </body>
        </html>";

    var options = new PdfRenderOptions 
    { 
        Policy = policy, // Bound to AllowModernLayout=true from appsettings
        TemplateId = "Modern-Flex-Template"
    };
    
    try
    {
        var (bytes, metadata) = await pdfService.RenderToBytesAsync(html, options, ct);
        return Results.File(bytes, "application/pdf", "modern-layout.pdf");
    }
    catch (PdfPolicyException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/pdf/render/strict", async (IMPdfService pdfService, DefaultStrictPolicy strictPolicy, CancellationToken ct) =>
{
    var html = @"
        <html>
            <head>
                <style>
                    body { font-family: sans-serif; }
                    .grid-container { display: grid; }
                </style>
            </head>
            <body>
                <div class='grid-container'>
                    <div>Grid Item</div>
                </div>
            </body>
        </html>";

    var options = new PdfRenderOptions 
    { 
        Policy = strictPolicy, // Ignores AllowModernLayout and blocks grid/flex
        TemplateId = "Strict-Template"
    };
    
    try
    {
        var (bytes, metadata) = await pdfService.RenderToBytesAsync(html, options, ct);
        return Results.File(bytes, "application/pdf", "strict-layout.pdf");
    }
    catch (Exception ex)
    {
        // Will throw PdfPolicyException because display:grid is blocked by DefaultStrictPolicy
        return Results.BadRequest(new { error = "Blocked by policy", details = ex.Message });
    }
});

app.Run();
