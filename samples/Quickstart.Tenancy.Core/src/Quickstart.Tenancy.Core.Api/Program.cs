WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// MultiTenant options setup
builder.Services.Configure<MultiTenantOptions>(o => 
{
    o.Enabled = true;
    o.Strategy = TenantIsolationStrategy.SeparateSchema;
});

// Ambient tenant context
builder.Services.AddScoped<ITenantContext, TenantContext>();

// Resolves tenant id from header (X-Tenant-ID)
builder.Services.AddScoped<ITenantIdResolver, DefaultTenantIdResolver>();

// Selects the schema based on schema format
builder.Services.AddSingleton<TenantSchemaSelector>();

// Quota tracking
builder.Services.AddTenantQuotaManagement();

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Tenancy.Core API",
        Version = "v1",
        Description = "Demonstrates Tenancy Core services."
    });
});

WebApplication app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

app.Use(async (context, next) => 
{
    // Simple custom middleware to set TenantContext from Resolver
    var resolver = context.RequestServices.GetRequiredService<ITenantIdResolver>();
    var tenantCtx = context.RequestServices.GetRequiredService<ITenantContext>();
    var tenantId = await resolver.ResolveTenantIdAsync(context);
    
    if (!string.IsNullOrEmpty(tenantId))
    {
        // Security validate the tenant ID (prevents schema injection)
        if (TenantSecurityValidator.TryValidate(tenantId, null, null, false, out string errorCode))
        {
            tenantCtx.TenantId = tenantId;
        }
    }
    
    await next();
});

app.MapGet("/api/tenant-info", (ITenantContext tenantContext, TenantSchemaSelector schemaSelector) =>
{
    var currentTenant = tenantContext.TenantId;
    if (string.IsNullOrEmpty(currentTenant)) return Results.BadRequest("No tenant context");
    
    // Schema rewriting based on SeparateSchema
    var schema = schemaSelector.ResolveSchema(currentTenant);
    
    return Results.Ok(new 
    { 
        Tenant = currentTenant,
        AssignedSchema = schema
    });
});

app.MapPost("/api/quota/consume", async (ITenantContext tenantContext, ITenantQuotaTracker tracker) =>
{
    var currentTenant = tenantContext.TenantId;
    if (string.IsNullOrEmpty(currentTenant)) return Results.BadRequest("No tenant context");

    // Track quota
    try 
    {
        await tracker.IncrementUsageAsync(currentTenant, QuotaType.ApiRequestsPerMinute, 1);
        return Results.Ok(new { Allowed = true, Resource = "api_calls" });
    }
    catch (QuotaExceededException)
    {
        return Results.Ok(new { Allowed = false, Resource = "api_calls" });
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
