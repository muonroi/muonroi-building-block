namespace Muonroi.RuleEngine.Runtime.Web.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.RuleEngine.Runtime.Web;
using Xunit;
using Moq;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.RuleEngine.Runtime.Tracing;
using Muonroi.RuleEngine.Core.Tracing;
using Microsoft.AspNetCore.Authorization;
using Muonroi.Core.Abstractions.Context;

public class RuleEngineWebTests
{
    [Fact]
    public async Task GetRuleSets_ShouldReturnSuccess()
    {
        // Arrange
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuthHandler>("Test", null);
                services.AddAuthorization();
                services.AddRouting();
                services.AddControllers(opt => 
                {
                    opt.Filters.Add(new AllowAnonymousFilter());
                })
                .AddApplicationPart(typeof(RuleEngineRuntimeEndpointExtensions).Assembly);
                
                services.AddSignalR();
                services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
                
                // Mock dependencies for RulesEngineService
                var storeMock = new Mock<IRuleSetStore>();
                storeMock.Setup(x => x.GetWorkflowsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<string> { "Workflow1" });
                storeMock.Setup(x => x.GetVersionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[] { 1 });
                storeMock.Setup(x => x.GetActiveVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);

                var service = new RulesEngineService(
                    storeMock.Object,
                    null, null, null, null, null, null, null);

                services.AddSingleton(service);
                services.AddSingleton(new Mock<IRuleSetAuditStore>().Object);
                
                // Mock dependencies for MapRuleTracingEndpoints
                services.AddSingleton(new Mock<IRuleDebuggerModeService>().Object);
                services.AddSingleton(new Mock<IRuleTraceStore>().Object);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapRuleEngineRuntimeWeb();
                });
            });

        using var server = new TestServer(builder);
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        // Act
        var response = await client.GetAsync("/api/v1/rule-engine/rulesets");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private class AllowAnonymousFilter : IAllowAnonymous, Microsoft.AspNetCore.Mvc.Filters.IAsyncAuthorizationFilter
    {
        public Task OnAuthorizationAsync(Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext context)
        {
            return Task.CompletedTask;
        }
    }

    private class TestAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder) : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "TestUser") };
            var identity = new System.Security.Claims.ClaimsIdentity(claims, "Test");
            var principal = new System.Security.Claims.ClaimsPrincipal(identity);
            var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(principal, "Test");

            return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
        }
    }
}
