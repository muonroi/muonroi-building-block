using Muonroi.Caching.Memory.MultiLevel;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Multi-level caching
// Registers IMemoryCache + IDistributedMemoryCache + IMultiLevelCacheService.
// When CacheType is Memory (default) the service uses in-process caches only
// and no external dependency is required.
// Switch CacheType to Redis in appsettings.json and enable the Redis section
// to use a real distributed cache backed by RedisCacheService.
// -------------------------------------------------------------------------
builder.Services.AddMultiLevelCaching(builder.Configuration);

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Caching API",
        Version = "v1",
        Description = "Demonstrates all Muonroi Caching package features: " +
                      "IMultiLevelCacheService (GetOrSetAsync, SetAsync, GetAsync, RemoveAsync) " +
                      "and DistributedCacheKeyBuilder."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
