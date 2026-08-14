WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// IMCacheService — required by RuleEngineAuthorizationPolicyEvaluator to cache
// authorization decisions. The shipped implementation is RedisCacheService;
// this sample registers an in-process IMemoryCache-backed implementation so it
// runs with no external dependency. Swap for AddRedisCache() in production.
// -------------------------------------------------------------------------
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IMCacheService, InMemoryCacheService>();

// -------------------------------------------------------------------------
// Muonroi.AuthZ — rule-engine-driven authorization
// AddMAuthorizationRuleEngine() registers:
//   IAuthorizationPolicyEvaluator → RuleEngineAuthorizationPolicyEvaluator
//   IAuthorizationHandler         → MuonroiAuthorizationHandler
//   IMRuleOrchestrator<AuthorizationRuleContext> (wraps RuleOrchestrator)
//   IRuleRowFilter<> + DefaultAuthRuleChangeHandler
// Authorization rules are then registered as IRule<AuthorizationRuleContext>.
// -------------------------------------------------------------------------
builder.Services.AddMAuthorizationRuleEngine();

// Register the example rule the evaluator will run for every decision.
builder.Services.AddScoped<IRule<AuthorizationRuleContext>, ManagerOnlyDeleteRule>();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.AuthZ API",
        Version = "v1",
        Description = "Demonstrates Muonroi.AuthZ rule-driven authorization: " +
                      "IAuthorizationPolicyEvaluator.EvaluateAsync over registered " +
                      "IRule<AuthorizationRuleContext> rules, returning AuthorizationResult."
    });
});

// AddMAuthorizationRuleEngine registers IAuthorizationPolicyEvaluator as a
// singleton that consumes the scoped IMRuleOrchestrator<AuthorizationRuleContext>
// (a captive dependency in the package). Disable build-time scope validation so
// the sample runs; a production host that needs strict scope validation should
// register the evaluator/orchestrator at matching lifetimes.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = false;
    options.ValidateOnBuild = false;
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
