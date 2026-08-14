using Muonroi.RuleEngine.Core;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRuleEngine<Quickstart.RuleEngine.Api.Models.OrderRequest>();
builder.Services.AddRulesFromAssemblies(typeof(Program).Assembly);

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
