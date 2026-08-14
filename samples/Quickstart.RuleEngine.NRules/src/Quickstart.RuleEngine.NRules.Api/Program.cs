WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// NRules engine  (Muonroi.RuleEngine.NRules — [FROZEN] package)
//
// AddNRulesEngine(configure, assemblies) compiles every NRules rule found in
// the supplied assemblies into a singleton NRulesEngine. RuleOptions (bound
// from the "NRules" section here via the configure callback) lets you disable
// a rule or pin it to a specific [Rule(name, version)].
//
// AddNRulesWeb() registers the package's own NRulesController
// (api/v1/rule-engine/nrules ...) as an MVC application part.
// -------------------------------------------------------------------------
builder.Services.AddNRulesEngine(
    configure: options => builder.Configuration.GetSection("NRules").Bind(options),
    assemblies: typeof(Program).Assembly);

builder.Services.AddNRulesWeb();

// NRulesController depends on IMDateTimeService from Muonroi.Core.
builder.Services.AddSingleton<IMDateTimeService, MDateTimeService>();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.RuleEngine.NRules API",
        Version = "v1",
        Description = "Demonstrates the [FROZEN] Muonroi.RuleEngine.NRules package: " +
                      "AddNRulesEngine() compiles NRules fluent rules (When/Then) with " +
                      "Muonroi [Rule(name, version)] versioning, and AddNRulesWeb() exposes " +
                      "the package NRulesController (CRUD + test). The sample OrdersController " +
                      "fires the NRulesEngine directly against an in-memory Order fact."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
