using Muonroi.RuleEngine.Core;
using RuleSourceGen.Api.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRuleEngine<DiscountRequest>();
builder.Services.AddRulesFromAssemblies(typeof(Program).Assembly);

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
