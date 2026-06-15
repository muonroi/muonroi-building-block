using Muonroi.AspNetCore.OpenApi.OpenApi;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.AspNetCore.OpenApi exposes two Swashbuckle IOperationFilter types:
//   - MErrorResponseFilter  : auto-documents 400 + 500 MErrorResponse on every endpoint.
//   - SwaggerDefaultValues  : fills parameter defaults/descriptions and prunes
//                             unsupported content types. It depends on
//                             IMJsonSerializeService to serialize default values.
// SwaggerDefaultValues requires IMJsonSerializeService — the concrete
// MJsonSerializeService lives in Muonroi.Core.
// -------------------------------------------------------------------------
builder.Services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.OpenApi API",
        Version = "v1",
        Description = "Demonstrates Muonroi.AspNetCore.OpenApi Swagger operation filters: " +
                      "MErrorResponseFilter (standard 400/500 error docs) and " +
                      "SwaggerDefaultValues (parameter defaults + content-type cleanup)."
    });

    // Register the Muonroi operation filters.
    options.OperationFilter<MErrorResponseFilter>();
    options.OperationFilter<SwaggerDefaultValues>();
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
