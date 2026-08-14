WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Logging.Abstractions API",
        Version = "v1",
        Description = "Demonstrates Logging Abstractions capabilities."
    });
});
WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/log", (IMLog<Program> logger) => {
    logger.LogInformation("This is a structured log message {Value}", 42);
    return Results.Ok();
});
app.Run();