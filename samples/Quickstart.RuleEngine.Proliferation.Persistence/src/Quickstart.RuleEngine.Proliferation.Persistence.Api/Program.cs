WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ProliferationDbContext>(options =>
    options.UseInMemoryDatabase("ProliferationDb"));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.RuleEngine.Proliferation.Persistence API",
        Version = "v1",
        Description = "Demonstrates RuleEngine.Proliferation.Persistence capabilities."
    });
});
WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();