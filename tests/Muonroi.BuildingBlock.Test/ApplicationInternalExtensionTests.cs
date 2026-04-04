using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class ApplicationInternalExtensionTests
{
    private static readonly Type ExtType = typeof(DefaultAuthContextFactory).Assembly
        .GetType("Muonroi.ApplicationInternalExtension")!;

    private static IServiceCollection Invoke(string name, IServiceCollection services, params object?[] args)
    {
        MethodInfo method = ExtType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .First(m => m.Name == name && m.GetParameters().Length == args.Length + 1 && !m.IsGenericMethod);
        object?[] parameters = [services, .. args];
        return (IServiceCollection)method.Invoke(null, parameters)!;
    }

    private static IServiceCollection InvokeGeneric(string name, Type generic, IServiceCollection services)
    {
        MethodInfo method = ExtType.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .First(m => m.Name == name && m.IsGenericMethod);
        MethodInfo genericMethod = method.MakeGenericMethod(generic);
        return (IServiceCollection)genericMethod.Invoke(null, [services])!;
    }

    private record DummyModel(string Name);

    private class DummyValidator : AbstractValidator<DummyModel>
    {
    }

    [Fact]
    public void AddControllerConfiguration_registers_services()
    {
        ServiceCollection services = [];
        _ = Invoke("AddControllerConfiguration", services, (object?)new[] { typeof(DummyValidator).Assembly });
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.Contains(services, s => s.ServiceType == typeof(RequestLoggingFilter));
        IOptions<MvcOptions> options = provider.GetRequiredService<IOptions<MvcOptions>>();
        Assert.Equal("BinderTypeModelBinderProvider", options.Value.ModelBinderProviders[0].GetType().Name);
    }

    [Fact]
    public void AddAuthContext_registers_expected_services()
    {
        ServiceCollection services = [];
        _ = Invoke("AddAuthContext", services);
        Assert.Contains(services, s => s.ServiceType == typeof(IHttpContextAccessor));
        ServiceDescriptor? desc = services.FirstOrDefault(s => s.ServiceType == typeof(IAuthContextFactory));
        Assert.NotNull(desc);
        Assert.Equal(typeof(DefaultAuthContextFactory), desc!.ImplementationType);
    }

    private class CustomFactory : IAuthContextFactory
    {
        public MAuthenticateInfoContext Create()
        {
            return new MAuthenticateInfoContext(false);
        }
    }

    [Fact]
    public void AddAuthContextFactory_registers_generic_factory()
    {
        ServiceCollection services = [];
        _ = InvokeGeneric("AddAuthContextFactory", typeof(CustomFactory), services);
        ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<CustomFactory>(provider.GetService<IAuthContextFactory>());
    }

    [Fact]
    public void AddAuthContextFactory_without_dependencies_throws()
    {
        ServiceCollection services = [];
        _ = InvokeGeneric("AddAuthContextFactory", typeof(DefaultAuthContextFactory), services);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.Throws<MInternalException>(() => provider.GetRequiredService<IAuthContextFactory>());
    }

    [Fact]
    public void AddJwtConfigs_registers_config_and_defaults()
    {
        Dictionary<string, string?> dict = new()
        {
            ["EnableEncryption"] = "false",
            ["TokenConfigs:SymmetricSecretKey"] = "keykeykeykeykeykeykeykeykeykeykey",
            ["TokenConfigs:Issuer"] = "issuer",
            ["TokenConfigs:Audience"] = "audience",
            ["TokenConfigs:EnableCookieAuth"] = "true"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        ServiceCollection services = [];
        MTokenInfo token = new()
        {
            CookieName = "",
            CookieSameSite = ""
        };
        _ = Invoke("AddJwtConfigs", services, config, token, true, "");
        ServiceProvider provider = services.BuildServiceProvider();

        MTokenInfo result = provider.GetRequiredService<MTokenInfo>();
        Assert.Equal("issuer", result.Issuer);
        Assert.Equal("audience", result.Audience);
        Assert.Equal("keykeykeykeykeykeykeykeykeykeykey", result.SymmetricSecretKey);
        Assert.Equal("AuthToken", result.CookieName);
        Assert.Equal("Lax", result.CookieSameSite);
    }

    [Fact]
    public void AddJwtConfigs_with_secretKey_false_branch()
    {
        Dictionary<string, string?> dict = new()
        {
            ["EnableEncryption"] = "false",
            ["TokenConfigs:SymmetricSecretKey"] = "key",
            ["TokenConfigs:Issuer"] = "iss",
            ["TokenConfigs:Audience"] = "aud"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        ServiceCollection services = [];
        _ = Invoke("AddJwtConfigs", services, config, null, false, "secret");
        ServiceProvider provider = services.BuildServiceProvider();

        MTokenInfo result = provider.GetRequiredService<MTokenInfo>();
        Assert.Equal("iss", result.Issuer);
    }

    [Fact]
    public void AddJwtConfigs_missing_values_throw()
    {
        Dictionary<string, string?> dict = new()
        {
            ["EnableEncryption"] = "false"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        ServiceCollection services = [];
        Invoke("AddJwtConfigs", services, config, null, true, "");
        ServiceProvider provider = services.BuildServiceProvider();
        MTokenInfo result = provider.GetRequiredService<MTokenInfo>();
        Assert.NotNull(result);
    }

    [Fact]
    public void AddPaginationConfigs_registers_config_instance()
    {
        Dictionary<string, string?> dict = new()
        {
            ["PaginationConfigs:DefaultPageIndex"] = "1",
            ["PaginationConfigs:DefaultPageSize"] = "10",
            ["PaginationConfigs:MaxPageSize"] = "100"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        ServiceCollection services = [];
        _ = Invoke("AddPaginationConfigs", services, config, null);
        ServiceProvider provider = services.BuildServiceProvider();

        MPaginationConfig result = provider.GetRequiredService<MPaginationConfig>();
        Assert.Equal(1, result.DefaultPageIndex);
        Assert.Equal(10, result.DefaultPageSize);
        Assert.Equal(100, result.MaxPageSize);
    }

    [Fact]
    public void AddPaginationConfigs_preserves_instance()
    {
        MPaginationConfig configObj = new();
        ServiceCollection services = [];
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        _ = Invoke("AddPaginationConfigs", services, config, configObj);
        ServiceProvider provider = services.BuildServiceProvider();
        MPaginationConfig result = provider.GetRequiredService<MPaginationConfig>();
        Assert.Same(configObj, result);
    }
}
