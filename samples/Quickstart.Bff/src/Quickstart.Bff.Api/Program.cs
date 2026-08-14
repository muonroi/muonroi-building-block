WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi.Bff — Backend-for-Frontend authentication
// AddBffAuthentication() (BffAuthenticationExtensions.AddBffAuthentication):
//   - configures cookie authentication (HttpOnly, Secure, SameSite=Strict)
//   - adds antiforgery (CSRF) protection with the same hardened cookie policy
//   - registers ITokenStore so refresh tokens stay server-side, never in the browser
// Pass useRedisTokenStore: true to swap InMemoryTokenStore for RedisTokenStore.
// Here we use the in-memory store (default) so no external dependency is needed.
// -------------------------------------------------------------------------
builder.Services.AddBffAuthentication(useRedisTokenStore: false);

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Bff API",
        Version = "v1",
        Description = "Demonstrates Muonroi.Bff: AddBffAuthentication() (cookie auth + antiforgery) " +
                      "and the server-side ITokenStore (InMemoryTokenStore) for SPA refresh tokens."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

 await app.RunAsync();
