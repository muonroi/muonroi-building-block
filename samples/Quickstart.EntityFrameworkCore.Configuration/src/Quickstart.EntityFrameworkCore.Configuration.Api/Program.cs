using Quickstart.EntityFrameworkCore.Configuration.Api;
using Muonroi.Data.EntityFrameworkCore.Entity;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextConfigure<AppDbContext, AppPermission>(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.EntityFrameworkCore.Configuration API",
        Version = "v1"
    });
});

WebApplication app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.OpenConnection();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/customers", async (AppDbContext db) => await db.Customers.ToListAsync());
app.MapPost("/customers", async (AppDbContext db, string name, string email) => 
{
    var c = new CustomerEntity { Id = Guid.NewGuid(), Name = name, Email = email };
    db.Customers.Add(c);
    await db.SaveChangesAsync();
    return Results.Ok(c);
});

app.Run();
