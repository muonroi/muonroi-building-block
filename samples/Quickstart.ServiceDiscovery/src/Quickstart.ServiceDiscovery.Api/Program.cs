using Muonroi.ServiceDiscovery.Consul.Consul;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.ServiceDiscovery.Consul
//
// Registration:
//   services.AddServiceDiscovery(configuration, environment)
//     - binds ConsulConfigs (section "ConsulConfigs") and registers it as a singleton
//     - registers IConsulClient ONLY when discovery is enabled, the environment is NOT
//       Development, and ServiceName + ConsulAddress are configured.
//
// Activation:
//   app.UseServiceDiscovery(environment)
//     - in non-Development with a Consul client present, deregisters then registers the
//       service with the Consul agent and schedules deregistration on shutdown.
//
// Both calls short-circuit safely in the Development environment, so this sample runs
// with NO Consul agent. Set ASPNETCORE_ENVIRONMENT=Production and a real ConsulAddress
// to actually register with Consul.
// -------------------------------------------------------------------------
builder.Services.AddServiceDiscovery(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.ServiceDiscovery API",
        Version = "v1",
        Description = "Demonstrates Muonroi.ServiceDiscovery.Consul: AddServiceDiscovery (binds " +
                      "ConsulConfigs + registers IConsulClient) and UseServiceDiscovery (registers " +
                      "the service instance with the Consul agent). No-ops safely in Development."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Registers this instance with Consul (no-op in Development / when not configured).
app.UseServiceDiscovery(app.Environment);

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
