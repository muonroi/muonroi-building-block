

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// --- Feature-specific registrations ---
// The aggregator depends on an ITenantQuotaStore. Host must provide it.
builder.Services.AddSingleton<ITenantQuotaStore, InMemoryTenantQuotaStore>();

// Register RecordOnlyBillingProvider and UsageAggregator
builder.Services.AddRecordOnlyBilling();

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Billing API",
        Version = "v1",
        Description = "Demonstrates Billing abstraction capabilities."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
