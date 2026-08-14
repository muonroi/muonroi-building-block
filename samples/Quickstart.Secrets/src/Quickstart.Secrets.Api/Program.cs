WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi secrets
// Muonroi.Secrets ships no DI extension method — it exposes the ISecretProvider
// contract and a ConfigurationSecretProvider implementation backed by IConfiguration.
// See src/Muonroi.Secrets/Secrets/ISecretProvider.cs:6 and
// src/Muonroi.Secrets/Secrets/ConfigurationSecretProvider.cs:9.
// Register the provider directly. In production, swap ConfigurationSecretProvider
// for a vault-backed ISecretProvider without touching consumers.
// -------------------------------------------------------------------------
builder.Services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Secrets API",
        Version = "v1",
        Description = "Demonstrates the Muonroi Secrets package: the ISecretProvider " +
                      "contract resolved via ConfigurationSecretProvider, which reads " +
                      "named secrets from IConfiguration (supports nested keys like Secrets:ApiKey)."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
