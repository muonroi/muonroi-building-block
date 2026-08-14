using FluentValidation;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;
using Muonroi.Mediator.Mediator;
using Quickstart.Mediator.Api.Pipeline;
using System.Reflection;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────────
// AddMuonroiLogging() registers IMLog<T>, IMLogContext, IMLogFactory, ILogScopeFactory.
builder.Services.AddLogging(lb => lb.AddMuonroiLogging());

// ── Execution-context ─────────────────────────────────────────────────────────
// ISystemExecutionContextAccessor propagates tenant/user/correlation data across the
// async call tree via AsyncLocal. Required by MAuthorizationBehavior and MTenantValidationBehavior.
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

// ── MMediator ─────────────────────────────────────────────────────────────────
// AddMMediator scans the executing assembly for:
//   IRequestHandler<,>        → commands & queries
//   INotificationHandler<>    → fan-out notification handlers
//   IStreamRequestHandler<,>  → streaming queries
//   IRequestPreProcessor<>    → pre-processors  (run by MPreProcessorBehavior)
//   IRequestPostProcessor<,>  → post-processors (run by MPostProcessorBehavior)
//
// AddMuonroiEcosystem() adds the built-in pipeline (inner-to-outer):
//   PostProcessor → PreProcessor → Validation → Authorization → TenantValidation
//   → Diagnostics → ExceptionHandler
//
// TimingBehavior<,> is a custom outer behavior added after the ecosystem behaviors.
builder.Services.AddMMediator(options =>
{
    options.Assemblies = [Assembly.GetExecutingAssembly()];

    // Built-in ecosystem behaviors (validation, auth, tenant, diagnostics, exception handling).
    options.AddMuonroiEcosystem();

    // Custom behavior — wraps every request with elapsed-time logging.
    options.AddBehavior(typeof(TimingBehavior<,>));
});

// ── FluentValidation ──────────────────────────────────────────────────────────
// Registers all AbstractValidator<T> implementations in the executing assembly.
// ValidationBehavior<TRequest, TResponse> (part of AddMuonroiEcosystem) resolves
// them automatically — no explicit wiring required.
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

// ── ASP.NET / Swagger ─────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Mediator",
        Version = "v1",
        Description = "Demonstrates IRequest, INotification, IStreamRequest, IPipelineBehavior, " +
                      "IRequestPreProcessor, IRequestPostProcessor, and MAuthorizeAttribute."
    });
});

WebApplication app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────
// Populate ISystemExecutionContextAccessor from incoming request headers so that
// MAuthorizationBehavior and MTenantValidationBehavior have a context to inspect.
app.Use(async (context, next) =>
{
    ISystemExecutionContextAccessor accessor =
        context.RequestServices.GetRequiredService<ISystemExecutionContextAccessor>();

    string? tenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    string? userId = context.Request.Headers["X-User-Id"].FirstOrDefault();

    // X-Permissions: comma-separated list, e.g. "orders:delete,orders:read"
    // MAuthorizationBehavior checks ISystemExecutionContext.Permissions against
    // the [MAuthorize(Permissions = "...")] attribute on the request class.
    string rawPermissions = context.Request.Headers["X-Permissions"].FirstOrDefault() ?? string.Empty;
    IReadOnlyList<string> permissions = rawPermissions
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    accessor.Set(new SystemExecutionContext(
        tenantId: tenantId,
        userId: userId,
        username: null,
        correlationId: context.TraceIdentifier,
        accessToken: null,
        apiKey: null,
        isAuthenticated: userId is not null,
        permissions: permissions,
        sourceType: "http"));

    try
    {
        await next();
    }
    finally
    {
        accessor.Clear();
    }
});

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
