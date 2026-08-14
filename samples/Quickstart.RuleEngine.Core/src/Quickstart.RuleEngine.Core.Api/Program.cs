WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// --- Feature-specific registrations ---
builder.Services.AddMRuleEngine<OrderContext>(options => 
{
    options.ExecutionMode = ExecutionMode.AllOrNothing;
});

// We can register rules using assembly scanning from Muonroi.RuleEngine.Core extension
builder.Services.AddRulesFromAssemblies(typeof(Program).Assembly);

// Add some required abstraction mocks for the sample
builder.Services.AddSingleton<IMDateTimeService, MockDateTimeService>();

// --- Standard wiring ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.RuleEngine.Core API",
        Version = "v1",
        Description = "Demonstrates RuleEngine.Core capabilities."
    });
});

WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.Run();

namespace Quickstart.RuleEngine.Core.Api
{
    public class MockDateTimeService : IMDateTimeService
    {
        public DateTime Now() => DateTime.Now;
        public DateTime UtcNow() => DateTime.UtcNow;
        public DateTime Today() => DateTime.Today;
        public DateTime UtcToday() => DateTime.UtcNow.Date;
        public double NowTs() => DateTimeOffset.Now.ToUnixTimeMilliseconds();
        public double UtcNowTs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
