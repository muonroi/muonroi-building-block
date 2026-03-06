using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muonroi.AspNetCore.Controllers;
using Muonroi.Tenancy;

namespace Muonroi.BuildingBlock.Test.Tenancy;

public sealed class TenantQuotaApiIntegrationTests
{
    [Fact]
    public async Task QuotaApi_WhenTenantMatches_ShouldReturnLimitsAndUpgrade()
    {
        await using WebApplication app = BuildApp();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-a");

        HttpResponseMessage limitsResponse = await client.GetAsync("/api/v1/tenants/tenant-a/quotas/limits");
        Assert.Equal(HttpStatusCode.OK, limitsResponse.StatusCode);

        HttpResponseMessage upgradeResponse = await client.PostAsJsonAsync(
            "/api/v1/tenants/tenant-a/quotas/upgrade",
            new UpgradeRequest(TenantTier.Enterprise));
        Assert.Equal(HttpStatusCode.OK, upgradeResponse.StatusCode);

        string upgradedPayload = await upgradeResponse.Content.ReadAsStringAsync();
        Assert.Contains("Enterprise", upgradedPayload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QuotaApi_WhenTenantMismatch_ShouldForbid()
    {
        await using WebApplication app = BuildApp();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "tenant-a");

        HttpResponseMessage response = await client.GetAsync("/api/v1/tenants/tenant-b/quotas/limits");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static WebApplication BuildApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(TenantQuotaController).Assembly);
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSingleton<ITenantQuotaStore, InMemoryTenantQuotaStore>();
        builder.Services.AddScoped<ITenantQuotaTracker, TenantQuotaTracker>();

        builder.Services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
        builder.Services.AddAuthorization();

        WebApplication app = builder.Build();
        app.Use(async (context, next) =>
        {
            string tenantId = context.Request.Headers.TryGetValue("X-Tenant-Id", out StringValues values)
                ? values.ToString()
                : string.Empty;
            TenantContext.CurrentTenantId = tenantId;
            try
            {
                await next();
            }
            finally
            {
                TenantContext.CurrentTenantId = null;
            }
        });

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        return app;
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            Claim[] claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimTypes.Name, "test-user"),
                new Claim(ClaimTypes.Role, "Admin")
            };

            ClaimsIdentity identity = new(claims, Scheme.Name);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
