using Quickstart.Data.EntityFrameworkCore.SqlServer.Api;
using Muonroi.Data.EntityFrameworkCore.Entity;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Register Muonroi DbContext using the provider
builder.Services.AddDbContextConfigure<AppDbContext, AppPermission>(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Data.EntityFrameworkCore.SqlServer API",
        Version = "v1",
        Description = "Demonstrates SqlServer capabilities."
    });
});

WebApplication app = builder.Build();

// For SQLite in-memory, we must ensure the database is created
if ("SqlServer" == "Sqlite") 
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.OpenConnection();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", dbType = "SqlServer" }));
app.MapGet("/samples", async (AppDbContext db) => 
{
    return Results.Ok(await db.Samples.ToListAsync());
});
app.MapPost("/samples", async (AppDbContext db, string name) => 
{
    var entity = new SampleEntity { Id = Guid.NewGuid(), Name = name };
    db.Samples.Add(entity);
    await db.SaveChangesAsync();
    return Results.Ok(entity);
});

app.Run();
