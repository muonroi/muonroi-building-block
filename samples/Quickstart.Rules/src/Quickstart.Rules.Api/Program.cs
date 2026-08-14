WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Register Rule dependencies
builder.Services.AddSingleton<FeatureFlagEvaluator>();

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Rules API",
        Version = "v1",
        Description = "Demonstrates standalone FEEL parsing and Feature Flags."
    });
});

WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();
