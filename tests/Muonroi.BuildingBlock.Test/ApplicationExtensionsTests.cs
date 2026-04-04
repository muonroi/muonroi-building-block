using Polly.Retry;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BuildingBlock.Test;

public class ApplicationExtensionsTests
{
    private sealed class TestPingRequest : IRequest<string>
    {
        public string? Message { get; set; }
    }

    private sealed class TestPingHandler : IRequestHandler<TestPingRequest, string>
    {
        public Task<string> Handle(TestPingRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"Pong{request.Message}");
        }
    }

    [Fact]
    public void AddApplication_Registers_Services()
    {
        ServiceCollection services = [];
        services.AddApplication(typeof(ApplicationExtensionsTests).Assembly);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<IMDateTimeService>());
        Assert.NotNull(provider.GetService<IHttpContextAccessor>());
    }

    [Fact]
    public void AddApplication_With_Null_Service_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<MArgumentException>(() =>
            services!.AddApplication(typeof(ApplicationExtensionsTests).Assembly));
    }

    [Fact]
    public void SwaggerConfig_Adds_SwaggerGen()
    {
        ServiceCollection services = [];
        services.SwaggerConfig("test");
        Assert.Contains(services, d => d.ServiceType == typeof(ISwaggerProvider));
    }

    [Fact]
    public void SwaggerConfig_Null_Service_Throws()
    {
        IServiceCollection? services = null;
        Assert.Throws<MArgumentException>(() => services!.SwaggerConfig("test"));
    }

    [Fact]
    public void AddLocalization_Success()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();
        builder.Services.AddSingleton<ResourceSetting>();
        WebApplication app = builder.Build();
        app.AddLocalization(typeof(ApplicationExtensionsTests).Assembly);
    }

    [Fact]
    public void AddLocalization_Missing_Service_Throws()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        WebApplication app = builder.Build();
        Assert.Throws<MInternalException>(() =>
            app.AddLocalization(typeof(ApplicationExtensionsTests).Assembly));
    }

    [Fact]
    public void AddAppConfiguration_Returns_Builder()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        WebApplicationBuilder result = builder.AddAppConfiguration();
        Assert.Same(builder, result);
    }

    [Fact]
    public void AddAppConfiguration_Null_Builder_Throws()
    {
        WebApplicationBuilder? builder = null;
        Assert.Throws<NullReferenceException>(() => builder!.AddAppConfiguration());
    }

    [Fact]
    public void SwaggerConfig_Registers_Swagger_With_Security_Scheme()
    {
        ServiceCollection services = [];

        string appName = typeof(ApplicationExtensionsTests).Assembly.GetName().Name!;
        Mock<IWebHostEnvironment> env = new();
        env.SetupGet(x => x.EnvironmentName).Returns("Development");
        env.SetupGet(x => x.ApplicationName).Returns(appName);
        env.SetupGet(x => x.ContentRootPath).Returns(Directory.GetCurrentDirectory());
        env.SetupGet(x => x.WebRootPath).Returns(Directory.GetCurrentDirectory());

        services.AddSingleton(env.Object);

        services.AddLogging();

        services.AddSingleton(sp =>
        {
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            return new ResiliencePipelineBuilder<HttpResponseMessage>()
                .ConfigureTelemetry(loggerFactory)
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .HandleResult(r => !r.IsSuccessStatusCode),
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(2)
                })
                .Build();
        });

        services.SwaggerConfig("TestService");
        services.AddControllers();

        ServiceProvider provider = services.BuildServiceProvider();

        ISwaggerProvider swaggerProvider = provider.GetRequiredService<ISwaggerProvider>();
        OpenApiDocument doc = swaggerProvider.GetSwagger("v1");

        Assert.Equal("v1", doc.Info.Version);
        Assert.Equal("TestService", doc.Info.Title);

        Assert.True(doc.Components.SecuritySchemes.ContainsKey("Bearer"));
        OpenApiSecurityScheme bearer = doc.Components.SecuritySchemes["Bearer"];
        Assert.Equal("JWT", bearer.BearerFormat);
        Assert.Equal("JWT Authorization header using the Bearer scheme.", bearer.Description);

        Assert.Contains(doc.SecurityRequirements, sr =>
            sr.Keys.Any(k => k.Reference.Id == "Bearer"));
    }

    [Fact]
    public async Task AddApplication_Scans_Assembly_For_Mediator_Handlers()
    {
        ServiceCollection services = [];
        services.AddSingleton<IList<string>>([]);
        services.AddApplication(typeof(ApplicationExtensionsTests).Assembly);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IMediator mediator = provider.GetRequiredService<IMediator>();

        TestPingRequest request = new()
        {
            Message = "42"
        };
        string response = await mediator.Send(request);

        Assert.Equal("Pong42", response);
    }
}
