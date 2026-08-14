WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.AspNetCore base API services
// AddBaseApi() (ServiceCollectionExtensions.AddBaseApi) wires up:
//   - API versioning (Asp.Versioning) with a default v1.0 and ReportApiVersions
//   - EndpointsApiExplorer + SwaggerGen
//   - ASP.NET Core health checks
// This is the minimal Muonroi hosting surface for a versioned, documented API.
// -------------------------------------------------------------------------
builder.Services.AddBaseApi();

// -------------------------------------------------------------------------
// MVC controllers + a descriptive Swagger document.
// (AddBaseApi already registered SwaggerGen with defaults; we add a doc here
//  to give the quickstart a readable title/description.)
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.AspNetCore API",
        Version = "v1",
        Description = "Demonstrates Muonroi.AspNetCore base hosting: AddBaseApi() " +
                      "(API versioning, Swagger, health checks)."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
