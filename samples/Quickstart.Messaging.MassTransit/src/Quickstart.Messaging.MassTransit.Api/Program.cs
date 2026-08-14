WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Context accessor required by MuonroiConsumerBase<T>
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
builder.Services.AddSingleton<ITenantContextPolicy, DefaultTenantContextPolicy>();

// --- MassTransit with InMemory transport ---
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    x.UsingInMemory((ctx, cfg) =>
    {
        cfg.ConfigureEndpoints(ctx);
    });
});

// Outbox relay - background service that drains event outbox
builder.Services.AddOutboxRelay();

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Messaging.MassTransit API",
        Version = "v1",
        Description = "Demonstrates MassTransit capabilities in Muonroi."
    });
});

WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();
