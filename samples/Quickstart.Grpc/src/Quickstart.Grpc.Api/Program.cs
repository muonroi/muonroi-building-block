using Muonroi.Core.Abstractions.Context;
using Muonroi.Grpc.Grpc;
using Quickstart.Grpc.Api.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.Grpc — server + client infrastructure
// AddGrpcServer() (GrpcHandler.AddGrpcServer) registers:
//   - the gRPC server with GrpcServerInterceptor (auth/tenant/telemetry),
//     message-size + compression options bound from the "GrpcServicesConfig" section
//   - GrpcRateLimiter, MTokenInfo, IContextResolver, ITenantContextPolicy
//   - a "grpc-runtime" health check
//
// NOTE (license): AddGrpcServer calls EnsureFeatureOrThrow(Premium.Grpc) and
// BaseGrpcService enforces the same feature per-call. gRPC is a Premium feature,
// so this throws at startup unless a license enabling it is present. The calls
// below are the REAL package API — run with a Grpc-enabled license to exercise.
// -------------------------------------------------------------------------
builder.Services.AddGrpcServer(builder.Configuration);

// BaseGrpcService needs an execution-context accessor to propagate
// correlation/tenant metadata onto outbound gRPC calls.
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

// A typed client service that derives from Muonroi.Grpc BaseGrpcService.
builder.Services.AddScoped<GreeterClientService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Grpc API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Grpc: AddGrpcServer() + UseGrpcTransport() and the " +
                      "BaseGrpcService client base (resilient unary calls with metadata propagation)."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// UseGrpcTransport() enables gRPC-Web when configured in GrpcServicesConfig.Server.
app.UseGrpcTransport(builder.Configuration);

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
