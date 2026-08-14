WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ITenantQuotaStore, InMemoryTenantQuotaStore>();
builder.Services.AddSingleton<ITenantQuotaTracker, InMemoryTenantQuotaTracker>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Quota API",
        Version = "v1",
        Description = "Demonstrates Quota capabilities."
    });
});
WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapPost("/consume", async (ITenantQuotaTracker tracker) => {
    // await tracker.RecordUsageAsync("tenant1", QuotaType.ApiCall, 1);
    return Results.Ok();
});
app.Run();