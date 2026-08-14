using System.Reflection;
using Muonroi.AspNetCore.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.AspNetCore.RuleEngine infrastructure
// AddRuleEngineInfrastructure() (RuleEngineInfrastructureExtensions.AddRuleEngineInfrastructure)
// registers, in one call:
//   - the rule engine store (AddRuleEngineStore, bound from "RuleStoreConfigs")
//   - IRuleChangeStore        -> InMemoryRuleChangeStore
//   - IRuleChangeProposalStore -> InMemoryRuleChangeProposalStore
//   - generic controller wiring (GenericControllerRouteConvention +
//     GenericControllerFeatureProvider) for MGenericController<TEntity, TDbContext>.
//
// NOTE (license): AddRuleEngineStore calls EnsureFeatureOrThrow(Premium.RuleEngine).
// The RuleEngine is a Premium feature, so this call throws at startup unless a
// license enabling it is present. The registration below is the REAL package API;
// run it with a RuleEngine-enabled license to exercise it end-to-end.
// -------------------------------------------------------------------------
builder.Services.AddRuleEngineInfrastructure(
    builder.Configuration,
    Assembly.GetExecutingAssembly());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.AspNetCore.RuleEngine API",
        Version = "v1",
        Description = "Demonstrates Muonroi.AspNetCore.RuleEngine: AddRuleEngineInfrastructure() " +
                      "(rule store, IRuleChangeStore, IRuleChangeProposalStore, generic controllers)."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
