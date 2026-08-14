WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi diagnostics tracing
// AddMuonroiDiagnostics() registers:
//   - IMTraceContext        (starts/accesses hierarchical trace sessions)
//   - ITraceSessionStore    (InMemoryTraceSessionStore — no external dependency)
// See src/Muonroi.Diagnostics/Extensions/MDiagnosticsServiceCollectionExtensions.cs:17
//
// MTraceContext depends on IMJsonSerializeService to serialize recorded payloads.
// AddCoreServices() normally provides it; here we register the default directly.
// See src/Muonroi.Diagnostics/Context/MTraceContext.cs:10
// -------------------------------------------------------------------------
builder.Services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();
builder.Services.AddMuonroiDiagnostics();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Diagnostics API",
        Version = "v1",
        Description = "Demonstrates the Muonroi Diagnostics package: IMTraceContext.Begin " +
                      "to open a trace session, ITraceSession.BeginNode for hierarchical nodes, " +
                      "Record for structured events, and Export to dump the trace tree."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
