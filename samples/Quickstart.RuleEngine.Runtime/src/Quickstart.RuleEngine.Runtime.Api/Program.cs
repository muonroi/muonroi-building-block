WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Need memory cache for RuleSetRuntimeCache
builder.Services.AddMemoryCache();

// Register the Rule Engine Runtime components
builder.Services.AddRuleEngineStore(builder.Configuration);

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.RuleEngine.Runtime API",
        Version = "v1",
        Description = "Demonstrates RuleEngine.Runtime capabilities."
    });
});

WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();
