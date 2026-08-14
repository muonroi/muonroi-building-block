WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? ruleDb = builder.Configuration.GetConnectionString("RuleDb");
if (string.IsNullOrWhiteSpace(ruleDb))
{
    throw new InvalidOperationException("ConnectionStrings:RuleDb is required.");
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddFeelWeb();
builder.Services.AddDecisionTableWeb(options =>
{
    options.PostgresConnectionString = ruleDb;
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
