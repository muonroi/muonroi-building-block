WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// IMLog<T> for MUiEngineSchemaNotifier.
builder.Services.AddLogging(lb => lb.AddMuonroiLogging());

// -------------------------------------------------------------------------
// Muonroi.SignalR — real-time UI engine schema notifications
// AddSignalRWithTenant() (SignalRServiceCollectionExtensions.AddSignalRWithTenant):
//   - calls AddSignalR()
//   - when "MultiTenantConfigs:Enabled" is true, registers TenantHubFilter (IHubFilter)
//     so hub invocations are scoped per tenant.
// MUiEngineHub is the hub clients connect to (group: schema watchers).
// IUiEngineSchemaNotifier -> MUiEngineSchemaNotifier broadcasts schema changes to that group.
// -------------------------------------------------------------------------
builder.Services.AddSignalRWithTenant(builder.Configuration);

// Register the notifier that pushes "SchemaChanged" events to subscribed clients.
builder.Services.AddSingleton<IUiEngineSchemaNotifier, MUiEngineSchemaNotifier>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.SignalR API",
        Version = "v1",
        Description = "Demonstrates Muonroi.SignalR: AddSignalRWithTenant(), the MUiEngineHub, " +
                      "and IUiEngineSchemaNotifier (MUiEngineSchemaNotifier) broadcasting schema changes."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

// Clients connect here and call SubscribeToSchemaChanges() to join the watcher group.
app.MapHub<MUiEngineHub>("/hubs/ui-engine");

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
