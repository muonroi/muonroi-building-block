using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.CEP;
using FraudDetection.Api.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCepWeb();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<FraudMonitorService>();

WebApplication app = builder.Build();

app.Use(async (context, next) =>
{
    ISystemExecutionContextAccessor accessor = context.RequestServices.GetRequiredService<ISystemExecutionContextAccessor>();
    string? tenantId = context.Request.Headers[CustomHeader.TenantId].FirstOrDefault();
    accessor.Set(new SystemExecutionContext(tenantId, null, null, context.TraceIdentifier, null, null, false, [], "http"));

    try
    {
        await next();
    }
    finally
    {
        accessor.Clear();
    }
});

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
