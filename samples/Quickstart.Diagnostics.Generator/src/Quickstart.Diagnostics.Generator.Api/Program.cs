var builder = WebApplication.CreateBuilder(args);

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Diagnostics.Generator API",
        Version = "v1",
        Description = "Demonstrates Source Generator for Diagnostics (MTraceable)."
    });
});

builder.Services.AddSingleton<DemoService>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();
