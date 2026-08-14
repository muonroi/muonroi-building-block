namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleEngineRuntimeEndpointExtensionsTests
{
    [Fact]
    public async Task MapRuleEngineRuntimeWeb_ShouldMapControllersTracingEndpoints_AndHub()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(MRuleFlowContractController).Assembly);
        builder.Services.AddSignalR();
        builder.Services.AddRuleEngineTracing();
        builder.Services.AddSingleton(Substitute.For<IConnectionMultiplexer>());
        builder.Services.AddSingleton(Substitute.For<IMJsonSerializeService>());
        builder.Services.AddSingleton(Substitute.For<IMRuleFlowContractProvider>());

        await using WebApplication app = builder.Build();
        app.MapRuleEngineRuntimeWeb();
        await app.StartAsync();

        EndpointDataSource dataSource = app.Services.GetRequiredService<EndpointDataSource>();
        string[] patterns = [.. dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)];

        patterns.Should().Contain("api/v1/rule-engine/rule-contracts/{sourceType}/{sourceCode}");
        patterns.Should().Contain("/muonroi/rule-debugger/{tenantId}/enable");
        patterns.Should().Contain(match => match.Contains("/hubs/ruleset-changes", StringComparison.Ordinal));
    }
}
