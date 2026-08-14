using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi structured logging
// AddMuonroiLogging() is an ILoggingBuilder extension that registers:
//   - IMLogContext        (ambient property bag, e.g. PushProperty)
//   - IMLog<T>            (generic structured logger with Info/Warn/Error/Debug)
//   - IMLogFactory        (creates IMLog instances by type or category name)
//   - ILogScopeFactory    (scoped logging)
// See src/Muonroi.Logging/MLogServiceCollectionExtensions.cs:16
// -------------------------------------------------------------------------
builder.Logging.AddMuonroiLogging();

// IMLog<T> / IMLogFactory depend on ISystemExecutionContextAccessor to enrich
// every log line with TenantId/UserId/CorrelationId from the ambient execution
// context. Muonroi.Core's AddCoreServices() registers this automatically; since
// this sample wires logging in isolation we register the default accessor here.
// See src/Muonroi.Logging/MLog.cs:14 and src/Muonroi.Logging/MLogFactory.cs:18
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Logging API",
        Version = "v1",
        Description = "Demonstrates the Muonroi Logging package: structured IMLog<T> " +
                      "(Info/Warn/Error/Debug), IMLogContext.PushProperty scopes, and " +
                      "IMLogFactory for creating loggers by type or category name."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
