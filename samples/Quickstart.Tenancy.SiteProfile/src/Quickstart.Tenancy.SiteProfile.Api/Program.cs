WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add multiple site profiles + resolver
builder.Services.AddMultiSiteProfiles(
    builder.Configuration,
    siteCodeAccessor: sp => 
    {
        var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
        return httpContext?.Request.Headers["X-Site-Code"].FirstOrDefault() ?? "sg01";
    typeof(Sg01Profile).Assembly
);

builder.Services.AddSiteResolvedService<IWelcomeService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Tenancy.SiteProfile API",
        Version = "v1",
        Description = "Demonstrates ISiteProfile, ISiteProfileResolver, and per-site service resolution."
    });
});

WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/welcome", (IWelcomeService welcomeService, ISiteProfileResolver resolver) =>
{
    return Results.Ok(new 
    { 
        CurrentSite = resolver.Current.SiteId,
        Message = welcomeService.GetWelcomeMessage()
    });
});

app.MapGet("/api/scope-test", (IServiceProvider sp) =>
{
    var usProfile = new Us01Profile();
    string scopeResult;
    using (SiteProfileScope.ForSite(usProfile))
    {
        var scopedResolver = sp.GetRequiredService<ISiteProfileResolver>();
        var scopedService = sp.GetRequiredService<IWelcomeService>();
        scopeResult = $"{scopedResolver.Current.SiteId} says {scopedService.GetWelcomeMessage()}";
    }
    return Results.Ok(new { scopedOutput = scopeResult });
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

// --- Types ---

public interface IWelcomeService { string GetWelcomeMessage(); }

public class Sg01Profile : ISiteProfile
{
    public string SiteId => "sg01";
    public bool IsEnabled => true;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeyedScoped<IWelcomeService, Sg01WelcomeService>(SiteId);
    }
}
public class Sg01WelcomeService : IWelcomeService { public string GetWelcomeMessage() => "Welcome to SG01!"; }

public class Us01Profile : ISiteProfile
{
    public string SiteId => "us01";
    public bool IsEnabled => true;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeyedScoped<IWelcomeService, Us01WelcomeService>(SiteId);
    }
}
public class Us01WelcomeService : IWelcomeService { public string GetWelcomeMessage() => "Welcome to US01!"; }
