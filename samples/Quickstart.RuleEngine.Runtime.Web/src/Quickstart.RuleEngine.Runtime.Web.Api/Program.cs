using Muonroi.RuleEngine.Runtime.Web;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Rule engine runtime web surface
//
// AddRuleEngineRuntimeWeb(configuration) registers the full runtime governance
// stack from Muonroi.RuleEngine.Runtime.Web:
//   - File-backed IRuleSetStore + RulesEngineService (via AddRuleEngineStore)
//   - Rule tracing services (RuleTracingOptions section)
//   - IRuleDryRunService + IMRuleFlowContractProvider
//   - SignalR hub support (RuleSetHubNotifier hosted service)
//   - The package's own MVC controllers registered as an application part:
//       RuntimeRuleSetController     (api/v1/rule-engine/rulesets)
//       MRuleFlowContractController  (rule flow contract metadata)
//       MRuleFlowExecuteController   (rule flow execution)
//
// Note: AddRuleEngineRuntimeWeb calls RequireMinimumTierFromProof(Licensed),
// so a Licensed activation proof is required at runtime. The file-backed store
// needs no database — rulesets are read from / written to the RuleStore path.
// -------------------------------------------------------------------------
builder.Services.AddRuleEngineRuntimeWeb(builder.Configuration);

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// AddControllers() is also called inside AddRuleEngineRuntimeWeb; calling it
// here is idempotent and lets this sample contribute its own controller.
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.RuleEngine.Runtime.Web API",
        Version = "v1",
        Description = "Demonstrates Muonroi.RuleEngine.Runtime.Web: " +
                      "AddRuleEngineRuntimeWeb() wires the runtime ruleset governance " +
                      "controllers (list / export / save / activate / validate / dry-run / audit), " +
                      "rule tracing endpoints, and the SignalR ruleset-change hub. " +
                      "The package controllers are guarded with [Authorize]; the sample " +
                      "SampleController below is anonymous and shows the runtime web surface is live."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// MapRuleEngineRuntimeWeb maps the package controllers, the rule tracing
// endpoints, and the SignalR hub at /hubs/ruleset-changes.
app.MapRuleEngineRuntimeWeb();

// MapControllers is also invoked by MapRuleEngineRuntimeWeb; the duplicate call
// is harmless and keeps this sample explicit about its own controllers.
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
