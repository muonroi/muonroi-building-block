using Muonroi.BackgroundJobs.Abstractions;
using Quickstart.BackgroundJobs.Quartz.Api.Jobs;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Execution context accessor required by TenantAwareJobBase
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
builder.Services.AddSingleton<ITenantContextPolicy, DefaultTenantContextPolicy>();

// --- Feature-specific registrations ---
// Automatically dispatch to Quartz based on appsettings "JobType": "Quartz"
// Quartz in-memory store is configured automatically by the package.
builder.Services.AddBackgroundJobs(builder.Configuration);

builder.Services.AddTransient<SampleTenantJob>();

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.BackgroundJobs.Quartz API",
        Version = "v1",
        Description = "Demonstrates Muonroi.BackgroundJobs.Quartz capabilities."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
