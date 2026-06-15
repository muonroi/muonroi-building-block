using Microsoft.EntityFrameworkCore;
using Muonroi.Mapping.Abstractions;
using Quickstart.Services.Api.Domain;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi generic service base
// Muonroi.Services exposes MServiceBase<TEntity,TDto> — an abstract EF Core CRUD
// base with virtual hooks (no DI extension method; it couples to DbContext by design).
// See src/Muonroi.Services/MServiceBase.cs:25.
//
// To use it we wire:
//   1. An EF Core DbContext (in-memory provider so no database is required).
//   2. An IEntityMapper<TEntity,TDto> the base depends on.
//   3. A concrete MServiceBase subclass (ProductService).
// -------------------------------------------------------------------------
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("quickstart-services"));

builder.Services.AddScoped<IEntityMapper<Product, ProductDto>, ProductMapper>();
builder.Services.AddScoped<ProductService>();

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Services API",
        Version = "v1",
        Description = "Demonstrates the Muonroi Services package: MServiceBase<TEntity,TDto> " +
                      "generic EF Core CRUD (CreateAsync/GetByIdAsync/UpdateAsync/DeleteAsync) " +
                      "with an overridden ApplyDefaultValues hook."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
