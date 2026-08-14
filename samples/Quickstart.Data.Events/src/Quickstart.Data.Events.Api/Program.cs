WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.Data.EntityFrameworkCore.Events
//
// This package adds mediator + messaging integration on top of the persistence
// core: saga persistence (MSagaDbContext) and the transactional outbox
// (MEventOutboxDbContext + SaveWithOutboxAsync).
//
// MSagaDbContext requires an IMediator for domain-event dispatch, so we register
// the Muonroi mediator first. No handlers are scanned — an empty pipeline is enough
// for the saga persistence demonstrated here.
// -------------------------------------------------------------------------
builder.Services.AddMMediator();

// -------------------------------------------------------------------------
// AddMuonroiSagaDbContext<TContext> — the package's primary saga registration.
// It forwards to EF Core's AddDbContext<TContext>(optionsAction). We use the
// in-memory provider so the saga store runs with NO real database; swap in
// UseNpgsql / UseSqlServer for production.
// -------------------------------------------------------------------------
builder.Services.AddMuonroiSagaDbContext<OrderSagaDbContext>(options =>
    options.UseInMemoryDatabase("quickstart-sagas"));

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Data.Events API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Data.EntityFrameworkCore.Events: saga persistence via " +
                      "AddMuonroiSagaDbContext<TContext> + MSagaDbContext (IMuonroiSaga, CorrelationId key, " +
                      "auto tenant + timestamp stamping) and the EventOutbox transactional-outbox entity."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
