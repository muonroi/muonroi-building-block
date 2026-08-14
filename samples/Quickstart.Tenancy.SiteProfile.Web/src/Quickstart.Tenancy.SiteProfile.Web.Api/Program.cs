WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Stub core dependencies for MRepository / MSiteRepository
builder.Services.AddSingleton<IDateTimeService>(new StubDateTimeService());
builder.Services.AddSingleton<IAuthContextAccessor>(new StubAuthContextAccessor());
builder.Services.AddSingleton<IMuonroiLicenseGuard>(new StubLicenseGuard());

// Use AddSiteInfrastructure
builder.Services.AddSiteInfrastructure(builder.Configuration, options =>
{
    // Access site code from header
    options.SiteCodeAccessor = sp => 
    {
        var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
        return httpContext?.Request.Headers["X-Site-Code"].FirstOrDefault() ?? "sg01";
    };
    options.SiteAssemblies = [typeof(Sg01Profile).Assembly];
    options.SkipStartupValidation = true; 
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Tenancy.SiteProfile.Web API",
        Version = "v1",
        Description = "Demonstrates AddSiteInfrastructure, SiteProfileStateMiddleware, and IMSiteRepository."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// Applies 503 if the current site profile is disabled
app.UseSiteProfileStateMiddleware();

app.MapGet("/api/samples", async (IMSiteRepository<Sg01DbContext, SampleEntity> repo, Sg01DbContext ctx) =>
{
    // Ensure DB created
    await ctx.Database.EnsureCreatedAsync();

    // MSiteRepository usage
    var items = await repo.DbSet.ToListAsync();
    return Results.Ok(items);
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();

// --- Types ---

public class SampleEntity { public Guid Id { get; set; } public string Name { get; set; } = string.Empty; }

public class Sg01DbContext : DbContext
{
    public Sg01DbContext(DbContextOptions<Sg01DbContext> options) : base(options) {}
    public DbSet<SampleEntity> Samples { get; set; }
}

public class Sg01SampleRepository : MSiteRepository<Sg01DbContext, SampleEntity>
{
    public Sg01SampleRepository(
        Sg01DbContext siteContext, 
        IAuthContextAccessor authContext, 
        IMuonroiLicenseGuard licenseGuard, 
        IDateTimeService dateTimeService) 
        : base(siteContext, authContext, licenseGuard, dateTimeService, null)
    {
    }
}

public class Sg01Profile : ISiteProfile
{
    public string SiteId => "sg01";
    public bool IsEnabled => true;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // 1. Register DbContext for this site (using SQLite in-memory for demo)
        services.AddSiteDbInfrastructure<Sg01DbContext>(options =>
        {
            options.UseSqlite("DataSource=:memory:");
        });

        // 2. Register repository for this site
        services.AddScoped<IMSiteRepository<Sg01DbContext, SampleEntity>, Sg01SampleRepository>();
    }
}

public class StubDateTimeService : IDateTimeService { public DateTime UtcNow => DateTime.UtcNow; public DateTimeOffset UtcNowOffset => DateTimeOffset.UtcNow; public DateTime LocalTime => DateTime.Now; }
public class StubAuthContextAccessor : IAuthContextAccessor { public IAuthContext? AuthContext => null; }
public class StubLicenseGuard : IMuonroiLicenseGuard { public void Assert(string featureKey) {} public bool IsFeatureEnabled(string featureKey) => true; }
