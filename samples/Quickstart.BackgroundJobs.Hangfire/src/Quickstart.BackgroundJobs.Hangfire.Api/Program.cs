WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Execution context accessor required by TenantAwareJobBase
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
builder.Services.AddSingleton<ITenantContextPolicy, DefaultTenantContextPolicy>();

// --- Feature-specific registrations ---
// Use in-memory storage for Hangfire for demo purposes
builder.Services.AddHangfire(config => config.UseMemoryStorage());
// Automatically dispatch to Hangfire based on appsettings "JobType": "Hangfire"
builder.Services.AddBackgroundJobs(builder.Configuration);

builder.Services.AddTransient<SampleTenantJob>();

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.BackgroundJobs.Hangfire API",
        Version = "v1",
        Description = "Demonstrates Muonroi.BackgroundJobs.Hangfire capabilities."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHangfireDashboard(); // Mapped to /hangfire by default

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
