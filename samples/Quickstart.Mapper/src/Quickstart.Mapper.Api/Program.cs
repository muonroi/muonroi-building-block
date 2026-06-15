using System.Reflection;
using Muonroi.Mapper.Mapper;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------------------
// Muonroi object mapper
// ConfigureMapper(params Assembly[]) scans the given assemblies for types
// implementing IMapFrom<T>, registers the convention-based MappingConfiguration,
// and registers IMapper -> SimpleMapper.
// See src/Muonroi.Mapper/Mapper/MapperServiceCollectionExtensions.cs:13
// Passing this assembly explicitly so the sample's ProductDto : IMapFrom<Product>
// pair is discovered.
// -------------------------------------------------------------------------
builder.Services.ConfigureMapper(Assembly.GetExecutingAssembly());

// -------------------------------------------------------------------------
// MVC controllers + Swagger
// -------------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Mapper API",
        Version = "v1",
        Description = "Demonstrates the Muonroi Mapper package: convention-based mapping " +
                      "via IMapper.Map<TDestination>(source) and Map<TSource,TDestination>(source, dest), " +
                      "with IMapFrom<T> registration discovered by ConfigureMapper()."
    });
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
