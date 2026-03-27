using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.RuleEngine.Runtime.Tracing;
using Muonroi.RuleEngine.Runtime.Web;
using Muonroi.RuleEngine.Runtime.Web.Controllers;
using Muonroi.RuleEngine.Runtime.Web.Services;
using NSubstitute;
using Xunit;

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
        builder.Services.AddSingleton(Substitute.For<IMRuleFlowContractProvider>());

        await using WebApplication app = builder.Build();
        app.MapRuleEngineRuntimeWeb();
        await app.StartAsync();

        EndpointDataSource dataSource = app.Services.GetRequiredService<EndpointDataSource>();
        string[] patterns = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        patterns.Should().Contain("api/v1/rule-engine/rule-contracts/{sourceType}/{sourceCode}");
        patterns.Should().Contain("/muonroi/rule-debugger/{tenantId}/enable");
        patterns.Should().Contain(match => match.Contains("/hubs/ruleset-changes", StringComparison.Ordinal));
    }
}
