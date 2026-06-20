// ─── Quickstart.BackgroundJobs — Program.cs ──────────────────────────────────
//
// This sample demonstrates all features of the Muonroi BackgroundJobs packages:
//
//   Feature                      API used
//   ──────────────────────────   ──────────────────────────────────────────────
//   Provider selection           BackgroundJobHandler.AddBackgroundJobs(cfg)
//   Fire-and-forget              IBackgroundJobScheduler.Enqueue<T>
//   Delayed one-shot             IBackgroundJobScheduler.Schedule<T>
//   Recurring (CRON)             IBackgroundJobScheduler.AddOrUpdateRecurring<T>
//   Cancel recurring             IBackgroundJobScheduler.RemoveRecurring
//   Tenant-aware jobs            TenantAwareJobBase (ReportEmailJob)
//   Plain jobs                   Simple class + RunAsync (DataCleanupJob)
//   Context restoration filter   JobContextActivatorFilter (auto-registered)
//   In-memory Hangfire storage   Hangfire.MemoryStorage
//   Dashboard                    /hangfire
//
// ─────────────────────────────────────────────────────────────────────────────

using Hangfire;
using Hangfire.MemoryStorage;
using Muonroi.BackgroundJobs.Abstractions;       // BackgroundJobHandler.AddBackgroundJobs (extension on IServiceCollection)
using Muonroi.Core.Abstractions.Context;         // ISystemExecutionContextAccessor, ITenantContextPolicy
using Quickstart.BackgroundJobs.Api.Jobs;

// Referencing Muonroi.BackgroundJobs.Hangfire is enough — its [ModuleInitializer]
// calls BackgroundJobHandler.RegisterProvider(JobType.Hangfire, ...) automatically
// the moment the assembly is loaded by the runtime.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── 1. Configure Hangfire with in-memory storage ──────────────────────────────
//
// In production replace UseMemoryStorage() with UsePostgreSqlStorage(),
// UseSqlServerStorage(), etc. and set ConnectionString in appsettings.json.
builder.Services.AddHangfire(config =>
{
    config
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseMemoryStorage(); // Hangfire.MemoryStorage — zero-dependency for the quickstart
});

// ── 2. Add Muonroi BackgroundJobs ─────────────────────────────────────────────
//
// Reads "BackgroundJobConfigs" section from appsettings.json:
//   { "JobType": "Hangfire", "ConnectionString": null }
//
// Dispatches to the Hangfire provider (registered by [ModuleInitializer]) which:
//   - registers JobContextActivatorFilter as a Hangfire server filter
//   - registers HangfireJobScheduler as IBackgroundJobScheduler (scoped)
//   - calls AddHangfireServer() for processing
builder.Services.AddBackgroundJobs(builder.Configuration);

// ── 3. Register job classes in DI ─────────────────────────────────────────────
//
// Hangfire resolves job instances from the ASP.NET Core DI container.
// Transient is the correct lifetime here — Hangfire creates a new scope per job execution.

// TenantAwareJobBase subclass: needs ISystemExecutionContextAccessor + ITenantContextPolicy.
// Both are already registered by Muonroi.Core via AddBackgroundJobs above.
builder.Services.AddTransient<ReportEmailJob>();

// Plain job class: only needs ILogger<DataCleanupJob> (provided by the framework).
builder.Services.AddTransient<DataCleanupJob>();

// ── 4. Standard ASP.NET Core services ────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.BackgroundJobs",
        Version = "v1",
        Description =
            "Demonstrates Muonroi BackgroundJobs: " +
            "Enqueue, Schedule, AddOrUpdateRecurring, RemoveRecurring. " +
            "Hangfire Dashboard available at /hangfire."
    });
});

// ─────────────────────────────────────────────────────────────────────────────
WebApplication app = builder.Build();

// ── 5. Hangfire middleware ────────────────────────────────────────────────────
//
// /hangfire — built-in job monitoring dashboard.
// Authorization is open for the quickstart; restrict in production via
//   DashboardOptions { Authorization = [...] }
app.UseHangfireDashboard("/hangfire");

// UseHangfireServer starts the background processing loop inside this process.
// In a horizontally-scaled deployment you may keep the dashboard-only instance
// and run dedicated worker processes with UseHangfireServer only.
#pragma warning disable CS0618 // UseHangfireServer(IApplicationBuilder) is the preferred simple overload; migration to AddHangfireServer() would require host builder restructuring beyond this quickstart's scope.
app.UseHangfireServer();
#pragma warning restore CS0618

// ── 6. Swagger + routing ──────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "BackgroundJobs v1"));
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
