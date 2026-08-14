WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// --- Feature-specific registrations ---
builder.Services.AddScribanTemplating();

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Quickstart.Templating API",
        Version = "v1",
        Description = "Demonstrates Scriban templating engine integration."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.MapPost("/render", async (ITemplateEngine templateEngine, [FromBody] RenderRequest request, CancellationToken ct) =>
{
    var variables = new Dictionary<string, object?>
    {
        { "Model", request.Model }
    };
    
    var result = await templateEngine.RenderAsync(request.Template, variables, ct);
    
    return Results.Text(result, "text/html");
});

app.Run();

public class RenderRequest
{
    public string Template { get; set; } = string.Empty;
    public object? Model { get; set; }
}
