namespace Muonroi.BuildingBlock.Test;

public class ConsulHandlerTests
{
    private class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "app";
        public string WebRootPath { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private class FakeLifetime : IHostApplicationLifetime
    {
        private static readonly CancellationTokenSource CancellationTokenSource = new();
        private readonly CancellationTokenSource _cts = CancellationTokenSource;
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _cts.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication()
        {
            _cts.Cancel();
        }
    }

    private class TestAddressesFeature : IServerAddressesFeature
    {
        public ICollection<string> Addresses { get; } = [];
        public bool PreferHostingUrls { get; set; }
    }

    [Fact]
    public void AddServiceDiscovery_Registers_Client_When_Config_Valid()
    {
        Dictionary<string, string?> cfg = new()
        {
            ["ConsulConfigs:ConsulAddress"] = "http://localhost:8500",
            ["ConsulConfigs:ServiceName"] = "svc"
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(cfg).Build();
        ServiceCollection services = [];
        FakeWebHostEnvironment env = new()
        {
            EnvironmentName = Environments.Production
        };
        services.AddServiceDiscovery(configuration, env);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ConsulConfigs>());
        Assert.NotNull(provider.GetService<IConsulClient>());
    }

    [Fact]
    public void AddServiceDiscovery_InDevelopment_Does_Not_Register_Client()
    {
        Dictionary<string, string?> cfg = new()
        {
            ["ConsulConfigs:ConsulAddress"] = "http://localhost:8500",
            ["ConsulConfigs:ServiceName"] = "svc"
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(cfg).Build();
        ServiceCollection services = [];
        FakeWebHostEnvironment env = new()
        {
            EnvironmentName = Environments.Development
        };
        services.AddServiceDiscovery(configuration, env);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ConsulConfigs>());
        Assert.Null(provider.GetService<IConsulClient>());
    }

    [Fact]
    public void AddServiceDiscovery_Missing_Config_Throws()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        ServiceCollection services = [];
        FakeWebHostEnvironment env = new();
        services.AddServiceDiscovery(configuration, env);
        ServiceProvider provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<ConsulConfigs>());
        Assert.Null(provider.GetService<IConsulClient>());
    }

    [Fact]
    public async Task UseServiceDiscoveryAsync_Registers_With_Consul()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        IAgentEndpoint agent = Substitute.For<IAgentEndpoint>();
        IConsulClient consul = Substitute.For<IConsulClient>();
        consul.Agent.Returns(agent);
        builder.Services.AddSingleton(consul);
        ConsulConfigs configs = new()
        {
            ServiceName = "svc",
            ServiceAddress = "127.0.0.1",
            ServicePort = 5000,
            ConsulAddress = "http://localhost:8500"
        };
        builder.Services.AddSingleton(configs);
        WebApplication app = builder.Build();
        await app.UseServiceDiscoveryAsync(builder.Environment);
        await agent.Received().ServiceDeregister(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await agent.Received().ServiceRegister(Arg.Any<AgentServiceRegistration>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UseServiceDiscoveryAsync_Registers_Service()
    {
        ConsulConfigs cfg = new()
        {
            ServiceName = "svc",
            ConsulAddress = "http://localhost:8500",
            ServiceAddress = "localhost",
            ServicePort = 8080
        };
        IConsulClient consul = Substitute.For<IConsulClient>();
        consul.Agent.Returns(Substitute.For<IAgentEndpoint>());
        IHostApplicationLifetime lifetime = Substitute.For<IHostApplicationLifetime>();
        ServiceCollection services = [];
        services.AddSingleton(cfg);
        services.AddSingleton(consul);
        services.AddSingleton(lifetime);
        IServiceProvider sp = services.BuildServiceProvider();
        ApplicationBuilder app = new(sp);
        TestAddressesFeature feature = new();
        feature.Addresses.Add("http://localhost:5000");
        app.ServerFeatures.Set<IServerAddressesFeature>(feature);
        IApplicationBuilder result = await app.UseServiceDiscoveryAsync(new FakeWebHostEnvironment());
        Assert.Same(app, result);
        await consul.Agent.Received().ServiceRegister(Arg.Any<AgentServiceRegistration>());
    }

    [Fact]
    public async Task UseServiceDiscoveryAsync_No_Config_Throws()
    {
        IConsulClient consul = Substitute.For<IConsulClient>();
        IHostApplicationLifetime lifetime = Substitute.For<IHostApplicationLifetime>();
        ServiceCollection services = [];
        services.AddSingleton(consul);
        services.AddSingleton(lifetime);
        IServiceProvider sp = services.BuildServiceProvider();
        ApplicationBuilder app = new(sp);
        IApplicationBuilder result = await app.UseServiceDiscoveryAsync(new FakeWebHostEnvironment());
        Assert.Same(app, result);
    }

    [Fact]
    public async Task UseServiceDiscoveryAsync_No_Service_Throws()
    {
        ConsulConfigs cfg = new()
        {
            ServiceName = "svc",
            ConsulAddress = "http://localhost:8500",
            ServiceAddress = "localhost",
            ServicePort = 8080
        };
        ServiceCollection services = [];
        services.AddSingleton(cfg);
        services.AddSingleton(Substitute.For<IHostApplicationLifetime>());
        IServiceProvider sp = services.BuildServiceProvider();
        ApplicationBuilder app = new(sp);
        IApplicationBuilder result = await app.UseServiceDiscoveryAsync(new FakeWebHostEnvironment());
        Assert.Same(app, result);
    }

    [Fact]
    public void UseServiceDiscovery_Registers_Service()
    {
        ConsulConfigs cfg = new()
        {
            ServiceName = "svc",
            ConsulAddress = "http://localhost:8500",
            ServiceAddress = "localhost",
            ServicePort = 8080
        };
        IConsulClient consul = Substitute.For<IConsulClient>();
        consul.Agent.Returns(Substitute.For<IAgentEndpoint>());
        IHostApplicationLifetime lifetime = new FakeLifetime();
        ServiceCollection services = [];
        services.AddSingleton(cfg);
        services.AddSingleton(consul);
        services.AddSingleton(lifetime);
        IServiceProvider sp = services.BuildServiceProvider();
        ApplicationBuilder app = new(sp);
        TestAddressesFeature feature = new();
        feature.Addresses.Add("http://localhost:5000");
        app.ServerFeatures.Set<IServerAddressesFeature>(feature);

        IApplicationBuilder result = app.UseServiceDiscovery(new FakeWebHostEnvironment());
        Assert.Same(app, result);
        consul.Agent.Received().ServiceRegister(Arg.Any<AgentServiceRegistration>());
    }

    [Fact]
    public void UseServiceDiscovery_No_Config_Throws()
    {
        IConsulClient consul = Substitute.For<IConsulClient>();
        IHostApplicationLifetime lifetime = Substitute.For<IHostApplicationLifetime>();
        ServiceCollection services = [];
        services.AddSingleton(consul);
        services.AddSingleton(lifetime);
        IServiceProvider sp = services.BuildServiceProvider();
        ApplicationBuilder app = new(sp);
        IApplicationBuilder result = app.UseServiceDiscovery(new FakeWebHostEnvironment());
        Assert.Same(app, result);
    }

    [Fact]
    public void UseServiceDiscovery_Duplicate_Service()
    {
        ConsulConfigs cfg = new()
        {
            ServiceName = "svc",
            ConsulAddress = "http://localhost:8500",
            ServiceAddress = "localhost",
            ServicePort = 8080
        };
        IConsulClient consul = Substitute.For<IConsulClient>();
        consul.Agent.Returns(Substitute.For<IAgentEndpoint>());
        IHostApplicationLifetime lifetime = new FakeLifetime();
        ServiceCollection services = [];
        services.AddSingleton(cfg);
        services.AddSingleton(consul);
        services.AddSingleton(lifetime);
        IServiceProvider sp = services.BuildServiceProvider();
        ApplicationBuilder app = new(sp);
        TestAddressesFeature feature = new();
        feature.Addresses.Add("http://localhost:5000");
        app.ServerFeatures.Set<IServerAddressesFeature>(feature);

        app.UseServiceDiscovery(new FakeWebHostEnvironment());
        app.UseServiceDiscovery(new FakeWebHostEnvironment());
        consul.Agent.Received(2).ServiceRegister(Arg.Any<AgentServiceRegistration>());
    }
}
