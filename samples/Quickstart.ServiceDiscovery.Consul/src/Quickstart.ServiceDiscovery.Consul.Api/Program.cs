WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.ServiceDiscovery.Consul API",
        Version = "v1",
        Description = "Demonstrates ServiceDiscovery.Consul capabilities."
    });
});
WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();