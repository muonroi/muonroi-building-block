using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class InfrastructureExtensionsExternalTests
{
    private static IConfiguration CreateConfig()
    {
        Dictionary<string, string?> cfg = new()
        {
            ["PaginationConfigs:DefaultPageIndex"] = "1",
            ["PaginationConfigs:DefaultPageSize"] = "10",
            ["PaginationConfigs:MaxPageSize"] = "100",
            ["TokenConfigs:SymmetricSecretKey"] = "testkey123456789012345678901234567890",
            ["TokenConfigs:Issuer"] = "iss",
            ["TokenConfigs:Audience"] = "aud",
            ["EnableEncryption"] = "false",
            ["LicenseConfigs:ProjectSeed"] = "test-project-seed-1234"
        };
        return new ConfigurationBuilder().AddInMemoryCollection(cfg).Build();
    }

    [Fact]
    public void AddInfrastructure_Registers_Services()
    {
        IClockProvider original = Clock.Provider;
        try
        {
            ServiceCollection services = [];
            IConfiguration config = CreateConfig();
            services.AddInfrastructure(config);
            services.AddInfrastructure(config);
            bool registered = services.Any(d => d.ServiceType == typeof(IAuthContextFactory));
            Assert.True(registered);
            Assert.Contains(services, d => d.ServiceType == typeof(ITenantIdResolver));
            Assert.Contains(services, d => d.ServiceType == typeof(TenantContextMiddleware));
        }
        finally
        {
            Clock.Provider = original;
        }
    }

    [Fact]
    public void AddInfrastructure_Registers_Middlewares_And_Filters()
    {
        IClockProvider original = Clock.Provider;
        try
        {
            ServiceCollection services = [];
            IConfiguration config = CreateConfig();
            services.AddInfrastructure(config);

            // MAuthenMiddleware<,> no longer exists - removed from infrastructure
            // var hasAuthMiddleware = services.Any(d =>
            bool hasCookieMiddleware = services.Any(d => d.ServiceType == typeof(MCookieAuthMiddleware));
            bool hasRequestLogging = services.Any(d => d.ServiceType == typeof(RequestLoggingFilter));

            // Assert.True(hasAuthMiddleware); // Commented out - MAuthenMiddleware removed
            Assert.True(hasCookieMiddleware);
            Assert.True(hasRequestLogging);
        }
        finally
        {
            Clock.Provider = original;
        }
    }

    [Fact]
    public void AddInfrastructure_NullServices_Throws()
    {
        IConfiguration config = CreateConfig();
        ServiceCollection? services = null;
        Assert.Throws<MArgumentException>(() => services!.AddInfrastructure(config));
    }

    [Fact]
    public void AddPermissionFilter_Registers_Filter()
    {
        ServiceCollection services = [];
        services.AddPermissionFilter<TestPerm>();
        services.AddPermissionFilter<TestPerm>();
        int count = services.Count(d => d.ServiceType == typeof(PermissionFilter<TestPerm>));
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<PermissionFilter<TestPerm>>());
        Assert.Equal(2, count);
    }

    [Fact]
    public void AddPermissionFilter_AddsFilterToMvcOptions()
    {
        ServiceCollection services = [];
        services.AddPermissionFilter<TestPerm>();
        ServiceProvider provider = services.BuildServiceProvider();

        IOptions<MvcOptions> options = provider.GetRequiredService<IOptions<MvcOptions>>();
        bool hasFilter = options.Value.Filters.OfType<ServiceFilterAttribute>()
            .Any(filter => filter.ServiceType == typeof(PermissionFilter<TestPerm>));

        Assert.True(hasFilter);
    }

    [Fact]
    public void AddPermissionFilter_NullServices_Throws()
    {
        ServiceCollection? services = null;
        Assert.Throws<MArgumentException>(() => services!.AddPermissionFilter<TestPerm>());
    }

    [Fact]
    public void AddDynamicPermission_Behaviors()
    {
        ServiceCollection services = [];
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("dynamic"));
        services.AddDynamicPermission<TestDbContext>();
        services.AddDynamicPermission<TestDbContext>();
        int count = services.Count(d => d.ServiceType == typeof(AuthorizePermissionFilter<TestDbContext>));
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<AuthorizePermissionFilter<TestDbContext>>());
        Assert.Equal(2, count);

        IServiceCollection? nullServices = null;
        Assert.Throws<MArgumentException>(() =>
            nullServices!.AddDynamicPermission<TestDbContext>());
    }

    [Fact]
    public void AddApiDocumentation_Behaviors()
    {
        ServiceCollection services = [];
        string xml = Path.Combine(AppContext.BaseDirectory,
            typeof(InfrastructureExtensionsExternalTests).Assembly.GetName().Name + ".xml");
        File.WriteAllText(xml, "<doc></doc>");
        services.AddApiDocumentation<InfrastructureExtensionsExternalTests>();
        services.AddApiDocumentation<InfrastructureExtensionsExternalTests>();
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ISwaggerProvider>());
        Assert.Contains(services, d => d.ServiceType == typeof(IConfigureOptions<SwaggerGenOptions>));

        IServiceCollection? nullServices = null;
        Assert.Throws<MArgumentException>(() =>
            nullServices!.AddApiDocumentation<InfrastructureExtensionsExternalTests>());
    }

    [Fact]
    public void AddCoreServices_Behaviors()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["EnableEncryption"] = "false" }).Build();
        ServiceCollection services = [];
        InvokeAddCoreServices(services, config);
        InvokeAddCoreServices(services, config);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IMJsonSerializeService>());
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(IMJsonSerializeService)));

        Assert.ThrowsAny<Exception>(() => InvokeAddCoreServices(null!, config));
    }

    private static IServiceCollection InvokeAddCoreServices(IServiceCollection services, IConfiguration config)
    {
        MethodInfo method =
            typeof(InfrastructureExtensions).GetMethod("AddCoreServices",
                BindingFlags.Static | BindingFlags.NonPublic)!;
        return (IServiceCollection)method.Invoke(null, [services, config, true, string.Empty, null, null])!;
    }

    [Fact]
    public void AddRedisConfiguration_Behaviors()
    {
        Dictionary<string, string?> data = new()
        {
            ["EnableEncryption"] = "false",
            ["RedisConfigs:Host"] = "localhost",
            ["RedisConfigs:Port"] = "6379",
            ["RedisConfigs:Password"] = "pwd",
            ["RedisConfigs:KeyPrefix"] = "pre"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        services.AddRedisConfiguration(config);
        services.AddRedisConfiguration(config);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<RedisConfigs>());
        Assert.Equal(2, services.Count(d => d.ServiceType == typeof(RedisConfigs)));

        Assert.Throws<MArgumentException>(() => InfrastructureExtensions.AddRedisConfiguration(null!, config));
    }

    [Fact]
    public void UseDefaultMiddleware_Behaviors()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        WebApplication app = builder.Build();
        IApplicationBuilder result1 = app.UseDefaultMiddleware();
        IApplicationBuilder result2 = app.UseDefaultMiddleware();
        Assert.Same(app, result1);
        Assert.Same(app, result2);

        Assert.Throws<MArgumentException>(() => InfrastructureExtensions.UseDefaultMiddleware(null!));
    }

    [Fact]
    public void AddConfigureHttpJson_Behaviors()
    {
        ServiceCollection services = [];
        services.AddConfigureHttpJson();
        ServiceProvider provider = services.BuildServiceProvider();
        JsonSerializerOptions opts = provider.GetRequiredService<IOptions<JsonSerializerOptions>>().Value;

        Assert.Equal(JsonNamingPolicy.CamelCase, opts.PropertyNamingPolicy);

        IServiceCollection? nullServices = null;
        Assert.Throws<MArgumentException>(() => nullServices!.AddConfigureHttpJson());
    }

    [Fact]
    public void ConfigureEndpoints_Behaviors()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddControllers();
        builder.Services.AddHealthChecks();
        WebApplication app = builder.Build();
        app.ConfigureEndpoints(true);
        app.ConfigureEndpoints(false);
    }

    [Fact]
    public void StaticEntryAssembly_IsInitialized()
    {
        Assembly?[] results = new Assembly?[5];
        Parallel.For(0, results.Length, i => results[i] = InfrastructureExtensions.EntryAssembly);
        Assert.All(results, r => Assert.Same(results[0], r));
    }
}
