using MassTransit;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;
using Muonroi.Messaging.MassTransit.Messaging;
using Quickstart.Messaging.Api.Consumers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Logging — registers IMLog<T> via ILoggingBuilder.AddMuonroiLogging()
// -------------------------------------------------------------------------
builder.Logging.AddMuonroiLogging();

// -------------------------------------------------------------------------
// Execution context — required by MuonroiConsumerBase<T>
// -------------------------------------------------------------------------
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

// -------------------------------------------------------------------------
// MassTransit — two modes controlled by "MessageBus:UseRabbitMq" in config.
//
//   useRabbitMq = true  → AddMessageBus() wires up the full Muonroi stack:
//                         RabbitMQ transport, consume/publish/send filters,
//                         OpenTelemetry, health checks, and outbox relay.
//                         Reads "MessageBusConfigs" section from appsettings.
//
//   useRabbitMq = false → Raw MassTransit in-memory bus.  No broker needed;
//                         ideal for running the sample locally without Docker.
//                         Consumers are registered manually so the same
//                         business logic is exercised either way.
// -------------------------------------------------------------------------
bool useRabbitMq = builder.Configuration.GetValue<bool>("MessageBus:UseRabbitMq");

if (useRabbitMq)
{
    // Full Muonroi messaging stack (requires a running RabbitMQ instance and a
    // valid Muonroi premium licence — AddMessageBus calls EnsureFeatureOrThrow).
    builder.Services.AddMessageBus(builder.Configuration, typeof(Program).Assembly);
    builder.Services.AddOutboxRelay();
}
else
{
    // In-memory transport — zero infrastructure, great for demos and CI.
    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<OrderCreatedConsumer>();
        x.AddConsumer<OrderShippedConsumer>();

        x.UsingInMemory((ctx, cfg) =>
        {
            cfg.ConfigureEndpoints(ctx);
        });
    });
}

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Messaging API",
        Version = "v1",
        Description =
            "Demonstrates Muonroi.Messaging.MassTransit features:\n" +
            "• MuonroiConsumerBase<T> — structured base consumer with context & error handling\n" +
            "• IPublishEndpoint / IBus — publishing events from controllers\n" +
            "• AddMessageBus() — full Muonroi stack (RabbitMQ, filters, OTEL, health)\n" +
            "• AddOutboxRelay() — persistent outbox background relay\n" +
            "Set \"MessageBus:UseRabbitMq\": true in appsettings.json to switch transports."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
